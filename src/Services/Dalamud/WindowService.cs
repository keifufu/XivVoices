namespace XivVoices.Services;

public interface IWindowService : IHostedService;

public class WindowService(ILogger _logger, ConfigWindow _configWindow, IDataService _dataService, WindowSystem _windowSystem, IDalamudPluginInterface _pluginInterface) : IWindowService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _windowSystem.AddWindow(_configWindow);

    _pluginInterface.UiBuilder.DisableCutsceneUiHide = true;
    _pluginInterface.UiBuilder.Draw += UiBuilderOnDraw;
    _pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    _pluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

    _dataService.OnOpenConfigWindow += OnOpenConfigWindow;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
    _pluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
    _pluginInterface.UiBuilder.Draw -= UiBuilderOnDraw;

    _dataService.OnOpenConfigWindow -= OnOpenConfigWindow;

    _windowSystem.RemoveAllWindows();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void OnOpenConfigWindow(object? sender, ConfigWindowTab tab)
  {
    _configWindow.IsOpen = true;
    _configWindow.SelectedTab = tab;
  }

  private void ToggleConfigUi()
  {
    _configWindow.Toggle();
  }

  private void UiBuilderOnDraw()
  {
    _windowSystem.Draw();
  }
}
