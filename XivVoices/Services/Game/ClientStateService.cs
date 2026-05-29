using Dalamud.Interface.ImGuiNotification;

namespace XivVoices.Services;

public interface IClientStateService : IHostedService { }

public class ClientStateService(ILogger _logger, IDataService _dataService, Configuration _configuration, IClientState _clientState, IDalamudPluginInterface _pluginInterface) : IClientStateService
{
  public Task StartAsync(CancellationToken token)
  {
    _clientState.Login += OnLogin;
    _dataService.OnLatestVersionChanged += WarnOutdated;

    // Don't want to warn outdated here, DataService will already do that OnLatestVersionChanged.
    WarnMuted();
    WarnRepository();

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken token)
  {
    _clientState.Login -= OnLogin;
    _dataService.OnLatestVersionChanged -= WarnOutdated;

    return _logger.ServiceLifecycle();
  }

  private void OnLogin()
  {
    WarnMuted();
    WarnOutdated();
    WarnRepository();
  }

  private void WarnMuted()
  {
    if (_configuration.MuteEnabled)
      _logger.Chat("Plugin is muted. \"/xivv mute\" to unmute.");
  }

  private void WarnOutdated()
  {
    // I don't want to have the dalamud toast pop up in the main menu.
    if (!_clientState.IsLoggedIn) return;

    if (_dataService.IsOutdated)
    {
      _logger.Chat(pre: "Plugin is outdated. Reports will not be processed.", preColor: 25);
      _logger.DalamudToast(NotificationType.Warning, "Plugin is outdated", "Reports will not be processed.", 15);
    }
  }

  private void WarnRepository()
  {
    if (_pluginInterface.SourceRepository.Trim().Contains("fantasticalmouthpiece", StringComparison.OrdinalIgnoreCase))
    {
      _logger.DalamudToast(NotificationType.Warning, "Deprecated Repository", "You are using a deprecated plugin repository URL for this plugin. Please migrate to 'https://xivv.keifufu.dev/repo'. Join our Discord for help.", 60);
    }
  }
}
