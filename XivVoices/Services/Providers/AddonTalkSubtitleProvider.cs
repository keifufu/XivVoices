using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace XivVoices.Services;

public interface IAddonTalkSubtitleProvider : IHostedService;

public class AddonTalkSubtitleProvider(ILogger _logger, IGameInteropService _gameInteropService, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IAddonLifecycle _addonLifecycle) : IAddonTalkSubtitleProvider
{
  private string _lastSentence = "";

  public Task StartAsync(CancellationToken token)
  {
    _addonLifecycle.RegisterListener(AddonEvent.PostDraw, "TalkSubtitle", OnTalkSubtitleAddonPostDraw);
    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken token)
  {
    _addonLifecycle.UnregisterListener(OnTalkSubtitleAddonPostDraw);
    return _logger.ServiceLifecycle();
  }

  private unsafe void OnTalkSubtitleAddonPostDraw(AddonEvent type, AddonArgs args)
  {
    AddonTalkSubtitle* addon = (AddonTalkSubtitle*)args.Addon.Address;
    if (addon == null) return;

    string sentence = _gameInteropService.ReadUtf8String(addon->SubtitleText);
    if (_lastSentence != sentence)
    {
      if (_selfTestService.Step == SelfTestStep.Provider_TalkSubtitle)
        _selfTestService.Report_Provider_TalkSubtitle(sentence);

      _lastSentence = sentence;
      _logger.Debug($"sentence::{sentence}");

      // Default these messages to the Narrator, individual ones can be overwritten via speaker mappings if needed.
      _ = _messageDispatcher.TryDispatch(MessageSource.AddonTalkSubtitle, "Narrator", sentence);
    }
  }
}
