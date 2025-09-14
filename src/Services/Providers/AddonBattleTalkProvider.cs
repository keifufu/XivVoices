using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivVoices.Services;

public interface IAddonBattleTalkProvider : IHostedService;

// PostRefresh is too early here? Guess I'm polling this one too.
public class AddonBattleTalkProvider(ILogger _logger, Configuration _configuration, IGameInteropService _gameInteropService, IPlaybackService _playbackService, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IFramework _framework, IAddonLifecycle _addonLifecycle) : PlaybackQueue(MessageSource.AddonBattleTalk, _logger, _configuration, _playbackService, _messageDispatcher, _gameInteropService, _framework), IAddonBattleTalkProvider
{
  private string _lastSpeaker = "";
  private string _lastSentence = "";

  public Task StartAsync(CancellationToken cancellationToken)
  {
    QueueStart();

    _addonLifecycle.RegisterListener(AddonEvent.PostDraw, "_BattleTalk", OnBattleTalkAddonPostDraw);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    QueueStop();

    _addonLifecycle.UnregisterListener(OnBattleTalkAddonPostDraw);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private unsafe void OnBattleTalkAddonPostDraw(AddonEvent type, AddonArgs args)
  {
    AddonBattleTalk* addon = (AddonBattleTalk*)args.Addon.Address;
    if (addon == null) return;

    string speaker = _gameInteropService.ReadTextNode(addon->Speaker);
    if (string.IsNullOrEmpty(speaker)) speaker = "Narrator";
    string sentence = _gameInteropService.ReadTextNode(addon->Sentence);

    if (_lastSpeaker != speaker || _lastSentence != sentence)
    {
      if (_selfTestService.Step == SelfTestStep.Provider_BattleTalk)
        _selfTestService.Report_Provider_BattleTalk(speaker, sentence);

      _lastSpeaker = speaker;
      _lastSentence = sentence;
      _logger.Debug($"speaker::{speaker} sentence::{sentence}");
      _ = EnqueueMessage(speaker, sentence);
    }
  }
}

[StructLayout(LayoutKind.Explicit, Size = 0x298)]
public unsafe struct AddonBattleTalk
{
  [FieldOffset(0x0)] public AtkUnitBase AtkUnitBase;
  [FieldOffset(0x238)] public AtkTextNode* Speaker;
  [FieldOffset(0x240)] public AtkTextNode* Sentence;
}
