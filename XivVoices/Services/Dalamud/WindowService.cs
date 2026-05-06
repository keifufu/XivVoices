namespace XivVoices.Services;

public interface IWindowService : IHostedService
{
  void OpenTab(ConfigTab tab, bool forceOpen = false);
  void Toggle();
}

public enum ConfigTab
{
  Overview,
  DialogueSettings,
  PlaybackSettings,
  OverlaySettings,
  AudioLogs,
  WineSettings,
  Debug,
  SelfTest,
}

public class WindowService(ILogger _logger, ConfigWindow _configWindow, IDataService _dataService, WindowSystem _windowSystem, IDalamudPluginInterface _pluginInterface
#if !NO_KTK
, ConfigAddon _configAddon
#endif
) : IWindowService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _windowSystem.AddWindow(_configWindow);

    _pluginInterface.UiBuilder.DisableCutsceneUiHide = true;
    _pluginInterface.UiBuilder.Draw += UiBuilderOnDraw;
    _pluginInterface.UiBuilder.OpenConfigUi += Toggle;
    _pluginInterface.UiBuilder.OpenMainUi += Toggle;
    _pluginInterface.UiBuilder.Draw += _dataService.FileDialogManager.Draw;

    _dataService.OnOpenConfigWindow += OnOpenConfigWindow;
    if (_dataService.DataDirectory == null) OnOpenConfigWindow(this, ConfigTab.Overview);

#if DEBUG
    OpenTab(ConfigTab.Overview);
#endif

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _pluginInterface.UiBuilder.OpenConfigUi -= Toggle;
    _pluginInterface.UiBuilder.OpenMainUi -= Toggle;
    _pluginInterface.UiBuilder.Draw -= UiBuilderOnDraw;

    _dataService.OnOpenConfigWindow -= OnOpenConfigWindow;

    _windowSystem.RemoveAllWindows();

    return _logger.ServiceLifecycle();
  }

  public void OpenTab(ConfigTab tab, bool forceOpen = false)
  {
    bool isDifferentTab = _configWindow.SelectedTab != tab;
#if NO_KTK
    _configWindow.SelectedTab = tab;
    _configWindow.IsOpen = forceOpen || isDifferentTab || !_configWindow.IsOpen;
#else
    _configAddon.SetTab(tab);
    bool shouldBeOpen = forceOpen || isDifferentTab || !_configAddon.IsOpen;
    if (shouldBeOpen) _configAddon.Open();
    else _configAddon.Close();
#endif
  }

  private void OnOpenConfigWindow(object? sender, ConfigTab tab)
  {
    OpenTab(tab, true);
  }

  public void Toggle()
  {
#if NO_KTK
    _configWindow.Toggle();
#else
    _configAddon.Toggle();
#endif
  }

  private void UiBuilderOnDraw()
  {
    _windowSystem.Draw();
  }
}
