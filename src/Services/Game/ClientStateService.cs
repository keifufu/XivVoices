namespace XivVoices.Services;

public interface IClientStateService : IHostedService { }

public class ClientStateService(ILogger _logger, Configuration _configuration, IClientState _clientState) : IClientStateService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _clientState.Login += WarnMuted;
    WarnMuted();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _clientState.Login -= WarnMuted;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void WarnMuted()
  {
    if (_configuration.MuteEnabled)
      _logger.Chat("Plugin is muted. \"/xivv mute\" to unmute.");
  }
}
