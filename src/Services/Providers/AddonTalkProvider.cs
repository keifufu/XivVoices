using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
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
public class AddonTalkProvider(ILogger _logger, Configuration _configuration, IPlaybackService _playbackService, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IGameInteropService _gameInteropService, IGameGui _gameGui, IKeyState _keyState, IFramework _framework, IGamepadState _gamepadState, IAddonLifecycle _addonLifecycle) : IAddonTalkProvider
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

  // Wanted to also use SPACE as a key here, as that's the usual auto-advance button,
  // but simply typing would then interrupt auto-advance.
  private unsafe bool CanAutoAdvance()
  {
    AtkUnitBasePtr addon = _gameGui.GetAddonByName("TalkAutoMessageSetting");
    return !_configuration.MuteEnabled
      && _configuration.AutoAdvanceEnabled
      && !_keyState[VirtualKey.MENU]
      && _gamepadState.Pressed(GamepadButtons.North) == 0
      && !addon.IsVisible;
  }

  private const uint AdvanceIconNodeId = 8;
  private const uint AutoAdvanceIconNodeId = 9;
  private unsafe void OnAddonTalkPreDraw(AddonEvent _, AddonArgs args)
  {
    AddonTalk* addon = (AddonTalk*)args.Addon.Address;
    if (CanAutoAdvance() && _playbackService.IsPlaying(MessageSource.AddonTalk))
    {
      AtkResNode* advanceIconNode = addon->UldManager.SearchNodeById(AdvanceIconNodeId);
      AtkResNode* autoAdvanceIconNode = addon->UldManager.SearchNodeById(AutoAdvanceIconNodeId);
      if (advanceIconNode == null) return;
      if (autoAdvanceIconNode == null) return;

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

  private unsafe (string, string) GetSpeakerAndSentence(AddonTalk* addon)
  {
    string speaker = _gameInteropService.ReadTextNode(addon->AtkTextNode220);
    if (string.IsNullOrEmpty(speaker)) speaker = "Narrator";
    string sentence = _gameInteropService.ReadUtf8String(addon->String268);
    return (speaker, sentence);
  }

  private unsafe void OnFrameworkUpdate(IFramework framework)
  {
    AddonTalk* addon = (AddonTalk*)_gameGui.GetAddonByName("Talk").Address;
    if (addon == null) return;

    bool visible = addon->IsVisible;
    if (visible != _lastVisible)
    {
      _lastVisible = visible;
      if (visible == false)
      {
        _logger.Debug("AddonTalk was clicked away.");
        if (!_configuration.QueueDialogue)
          _playbackService.Stop(MessageSource.AddonTalk);
        _lastSpeaker = "";
        _lastSentence = "";
      }
    }

    if (!visible) return;

    (string speaker, string sentence) = GetSpeakerAndSentence(addon);

    if (_lastSpeaker != speaker || _lastSentence != sentence)
    {
      if (_selfTestService.Step == SelfTestStep.Provider_Talk)
        _selfTestService.Report_Provider_Talk(speaker, sentence);

      _lastSpeaker = speaker;
      _lastSentence = sentence;
      _logger.Debug($"speaker::{speaker} sentence::{sentence}");

      // We only allow one AddonTalk line to play at a time
      if (!_configuration.QueueDialogue)
        _playbackService.Stop(MessageSource.AddonTalk);

      _ = _messageDispatcher.TryDispatch(MessageSource.AddonTalk, speaker, sentence);
    }
  }

  private void OnPlaybackCompleted(object? sender, XivMessage message)
  {
    if (message.Source != MessageSource.AddonTalk) return;
    _logger.Debug("AddonTalk Playback Completed.");
    AutoAdvance(message);
  }

  public unsafe void AutoAdvance(XivMessage message)
  {
    if (!CanAutoAdvance()) return;

    _gameInteropService.RunOnFrameworkThread(() =>
    {
      AddonTalk* addon = (AddonTalk*)_gameGui.GetAddonByName("Talk").Address;
      if (addon == null) return;
      if (!addon->IsVisible) return;

      (string speaker, string sentence) = GetSpeakerAndSentence(addon);
      if (speaker != message.RawSpeaker || sentence != message.RawSentence)
      {
        _logger.Debug("addontalk speaker or sentence changed, not auto-advancing.");
        return;
      }

      AtkEvent* evt = stackalloc AtkEvent[1]
      {
        new()
        {
          Listener = (AtkEventListener*)addon,
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
      addon->ReceiveEvent(AtkEventType.MouseDown, 0, evt, data);
      addon->ReceiveEvent(AtkEventType.MouseClick, 0, evt, data);
      addon->ReceiveEvent(AtkEventType.MouseUp, 0, evt, data);
    });
  }
}
