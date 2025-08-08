using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivVoices.Services;

public interface IAddonBattleTalkProvider : IHostedService;

// PostRefresh is too early here? Guess I'm polling this one too.
public class AddonBattleTalkProvider(ILogger _logger, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IGameInteropService _gameInteropService, IAddonLifecycle _addonLifecycle) : IAddonBattleTalkProvider
{
  private string _lastSpeaker = "";
  private string _lastSentence = "";

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _addonLifecycle.RegisterListener(AddonEvent.PostDraw, "_BattleTalk", OnBattleTalkAddonPostDraw);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
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
      _ = _messageDispatcher.TryDispatch(MessageSource.AddonBattleTalk, speaker, sentence);
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
