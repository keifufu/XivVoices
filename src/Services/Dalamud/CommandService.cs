namespace XivVoices.Services;

public interface ICommandService : IHostedService;

public class CommandService(ILogger _logger, Configuration _configuration, ConfigWindow _configWindow, ICommandManager _commandManager, IPlaybackService _playbackService, IDataService _dataService) : ICommandService
{
  private const string XivVoicesCommand = "/xivvoices";
  private const string XivVoicesCommandAlias = "/xivv";

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _commandManager.AddHandler(XivVoicesCommand, new CommandInfo(OnCommand)
    {
      HelpMessage = $"See '{XivVoicesCommand} help' for more."
    });
    _commandManager.AddHandler(XivVoicesCommandAlias, new CommandInfo(OnCommand)
    {
      HelpMessage = $"Alias for {XivVoicesCommand}."
    });

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _commandManager.RemoveHandler(XivVoicesCommand);
    _commandManager.RemoveHandler(XivVoicesCommandAlias);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void OnCommand(string command, string arguments)
  {
    _logger.Debug($"command::'{command}' arguments::'{arguments}'");

    string[] args = arguments.Split(" ", StringSplitOptions.RemoveEmptyEntries);
    if (args.Length == 0)
    {
      _configWindow.Toggle();
      return;
    }

    switch (args[0])
    {
      case "help":
        _logger.Chat("Available commands:");
        _logger.Chat($"  {command} help - Display this help menu");
        _logger.Chat($"  {command} version - Print the plugin's version");
        _logger.Chat($"  {command} mute - Toggle the muted state");
        _logger.Chat($"  {command} skip - Skip the latest playing voiceline");
        _logger.Chat($"  {command} settings - Open the settings window");
        _logger.Chat($"  {command} overview - Open the overview tab");
        _logger.Chat($"  {command} dialogue - Open the dialogue settings tab");
        _logger.Chat($"  {command} audio - Open the audio settings tab");
        _logger.Chat($"  {command} logs - Open the audio logs tab");
        if (Util.IsWine())
          _logger.Chat($"  {command} wine - Open the wine settings tab");
        if (_configuration.DebugMode)
        {
          _logger.Chat($"  {command} debug - Open the debug tab");
          _logger.Chat($"  {command} selftest - Open the self-test tab");
        }
        _logger.Chat($"  {command}");
        break;
      case "version":
        _logger.Chat($"v{_dataService.Version} / v{_dataService.LatestVersion}");
        break;
      case "mute":
        bool mute = !_configuration.MuteEnabled;
        _configuration.MuteEnabled = mute;
        _configuration.Save();
        if (mute) _playbackService.StopAll();
        _logger.Chat(mute ? "Muted" : "Unmuted");
        break;
      case "skip":
        _playbackService.Skip();
        break;
      case "settings":
        _configWindow.Toggle();
        break;
      case "overview":
        SwitchTab(ConfigWindowTab.Overview);
        break;
      case "dialogue":
        SwitchTab(ConfigWindowTab.DialogueSettings);
        break;
      case "audio":
        SwitchTab(ConfigWindowTab.AudioSettings);
        break;
      case "logs":
        SwitchTab(ConfigWindowTab.AudioLogs);
        break;
      case "wine":
        SwitchTab(ConfigWindowTab.WineSettings);
        break;
      case "debug":
        SwitchTab(ConfigWindowTab.Debug);
        break;
      case "selftest":
        SwitchTab(ConfigWindowTab.SelfTest);
        break;
      default:
        _logger.Chat("Invalid command:");
        _logger.Chat($"  {command} {arguments}");
        goto case "help";
    }
  }

  private void SwitchTab(ConfigWindowTab tab)
  {
    bool isDifferentTab = _configWindow.SelectedTab != tab;
    _configWindow.SelectedTab = tab;
    _configWindow.IsOpen = isDifferentTab || !_configWindow.IsOpen;
  }
}
