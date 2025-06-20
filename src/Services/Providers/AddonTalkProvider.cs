using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivVoices.Services;

public interface IAddonTalkProvider : IHostedService;

// For now, this is polled as i can't find a way to know when the second part of a multi-part
// sentence is reached via AddonLifecycles.
// PostRefresh is fired once per line.
// If we do end up regenerating all lines to be the full multiline-text, my best guess as to how
// Auto-Advance should work would be: just fucking continue to the next slide at like 75% of the line
// being played or something like that.
public class AddonTalkProvider(ILogger _logger, Configuration _configuration, IPlaybackService _playbackService, IMessageDispatcher _messageDispatcher, IGameGui _gameGui, IKeyState _keyState, IFramework _framework, IGamepadState _gamepadState, IAddonLifecycle _addonLifecycle) : IAddonTalkProvider
{
  private bool _lastVisible = false;
  private string _lastSpeaker = "";
  private string _lastSentence = "";

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _framework.Update += OnFrameworkUpdate;
    _playbackService.PlaybackCompleted += OnPlaybackCompleted;
    _addonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", OnAddonTalkPreDraw);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _framework.Update -= OnFrameworkUpdate;
    _playbackService.PlaybackCompleted -= OnPlaybackCompleted;
    _addonLifecycle.UnregisterListener(AddonEvent.PreDraw, "Talk", OnAddonTalkPreDraw);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private unsafe bool CanAutoAdvance()
  {
    AtkUnitBase* addon = (AtkUnitBase*)_gameGui.GetAddonByName("TalkAutoMessageSetting");
    bool addonVisible = addon != null && addon->IsVisible;
    return !_configuration.MuteEnabled
      && _configuration.AutoAdvanceEnabled
      && !_keyState[VirtualKey.MENU]
      && !_keyState[VirtualKey.SPACE]
      && _gamepadState.Pressed(GamepadButtons.North) == 0
      && !addonVisible;
  }

  private const uint AdvanceIconNodeId = 8;
  private const uint AutoAdvanceIconNodeId = 9;
  private unsafe void OnAddonTalkPreDraw(AddonEvent _, AddonArgs args)
  {
    AddonTalk* addon = (AddonTalk*)args.Addon;
    if (CanAutoAdvance() && _playbackService.IsPlaying(MessageSource.AddonTalk))
    {
      AtkResNode* advanceIconNode = addon->UldManager.SearchNodeById(AdvanceIconNodeId);
      AtkResNode* autoAdvanceIconNode = addon->UldManager.SearchNodeById(AutoAdvanceIconNodeId);

      if (advanceIconNode->IsVisible())
      {
        advanceIconNode->NodeFlags &= ~NodeFlags.Visible;
        advanceIconNode->DrawFlags |= 0x1;
        autoAdvanceIconNode->NodeFlags |= NodeFlags.Visible;
        autoAdvanceIconNode->DrawFlags |= 0x1;

        float rotation = autoAdvanceIconNode->Rotation;
        rotation += MathF.PI * (float)_framework.UpdateDelta.TotalSeconds;
        if (rotation > 2 * MathF.PI) rotation = 0;

        autoAdvanceIconNode->Rotation = rotation;
        autoAdvanceIconNode->DrawFlags |= 0xD;
      }
    }
  }

  private unsafe void OnFrameworkUpdate(IFramework framework)
  {
    AddonTalk* addon = (AddonTalk*)_gameGui.GetAddonByName("Talk");
    if (addon == null) return;

    bool visible = addon->AtkUnitBase.IsVisible;
    if (visible != _lastVisible)
    {
      _lastVisible = visible;
      if (visible == false)
      {
        _logger.Debug("AddonTalk was clicked away.");
        _playbackService.Stop(MessageSource.AddonTalk);
        _lastSpeaker = "";
        _lastSentence = "";
      }
    }

    if (!visible) return;

    string speaker = ReadTextNode(addon->AtkTextNode220);
    if (string.IsNullOrEmpty(speaker)) speaker = "AddonTalk";
    string sentence = ReadUtf8String(addon->String268);

    if (_lastSpeaker != speaker || _lastSentence != sentence)
    {
      _lastSpeaker = speaker;
      _lastSentence = sentence;
      _logger.Debug($"speaker::{speaker} sentence::{sentence}");
      _ = _messageDispatcher.TryDispatch(MessageSource.AddonTalk, speaker, sentence);
    }
  }

  private void OnPlaybackCompleted(object? sender, MessageSource source)
  {
    if (source != MessageSource.AddonTalk) return;
    _logger.Debug("AddonTalk Playback Completed.");
    AutoAdvance();
  }

  private static unsafe string ReadTextNode(AtkTextNode* textNode)
  {
    if (textNode == null) return "";
    SeString seString = textNode->NodeText.StringPtr.AsDalamudSeString();
    return seString.TextValue
      .Trim()
      .Replace("\n", "")
      .Replace("\r", "");
  }

  private static unsafe string ReadUtf8String(Utf8String str)
  {
    return new Lumina.Text.ReadOnly.ReadOnlySeString(str)
      .ExtractText()
      .Trim()
      .Replace("\n", "")
      .Replace("\r", "");
  }

  public unsafe void AutoAdvance()
  {
    if (!CanAutoAdvance()) return;

    AddonTalk* addonTalk = (AddonTalk*)_gameGui.GetAddonByName("Talk");
    if (addonTalk == null) return;
    if (!addonTalk->IsVisible) return;

    _framework.RunOnFrameworkThread(() =>
    {
      AddonTalk* addonTalk = (AddonTalk*)_gameGui.GetAddonByName("Talk");
      if (addonTalk == null) return;
      AtkEvent* evt = stackalloc AtkEvent[1]
      {
        new()
        {
          Listener = (AtkEventListener*)addonTalk,
          Target = &AtkStage.Instance()->AtkEventTarget,
          State = new()
          {
            StateFlags = (AtkEventStateFlags)132
          }
        }
      };
      AtkEventData* data = stackalloc AtkEventData[1];
      for (int i = 0; i < sizeof(AtkEventData); i++)
      {
        ((byte*)data)[i] = 0;
      }
      addonTalk->ReceiveEvent(AtkEventType.MouseDown, 0, evt, data);
      addonTalk->ReceiveEvent(AtkEventType.MouseClick, 0, evt, data);
      addonTalk->ReceiveEvent(AtkEventType.MouseUp, 0, evt, data);
    });
  }
}
