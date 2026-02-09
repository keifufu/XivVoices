using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace XivVoices.Services;

public interface ICommandService : IHostedService;

public class CommandService(ILogger _logger, Configuration _configuration, ConfigWindow _configWindow, IDataService _dataService, IPlaybackService _playbackService, IMessageDispatcher _messageDispatcher, IGameInteropService _gameInteropService, ICommandManager _commandManager) : ICommandService
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

  private async void OnCommand(string command, string arguments)
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
      case "?":
        _logger.Chat("Available commands:");
        _logger.Chat($"  {command} help - Display this help menu");
        _logger.Chat($"  {command} version - Print the plugin's version");
        _logger.Chat($"  {command} upload-logs - Uploads plugin logs to help with bug-reports");
        _logger.Chat($"  {command} mute - Toggle the muted state");
        _logger.Chat($"  {command} pause - Pauses voicelines until executed again");
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
          _logger.Chat($"  {command} target-info - Prints debug info about the current target");
        }
        _logger.Chat($"  {command}");
        break;
      case "target-info":
        unsafe
        {
          string targetName = _gameInteropService.GetTarget()?.Name.ToString() ?? "";
          Character* target = (Character*)_gameInteropService.TryFindCharacter(targetName, 0);
          if (target == null)
          {
            _logger.Chat("No target found.");
            return;
          }

          string race = _gameInteropService.GetBeastmanRace(target);
          _logger.Chat(race);
        }
        break;
      case "version":
        _logger.Chat($"v{_dataService.Version} / v{_dataService.LatestVersion}");
        break;
      case "upload-logs":
        _logger.Chat("Uploading logs...");
        (bool success, string body) = await _dataService.UploadLogs();
        if (success)
        {
          string url = $"{_dataService.ServerUrl}/{body}";
          ImGui.SetClipboardText(url);
          _logger.Chat($"Uploaded logs to: '{url}'. Copied to clipboard!");
        }
        else
        {
          _logger.Chat($"Failed to upload logs. Reason: {body}");
        }
        break;
      case "mute":
        bool mute = !_configuration.MuteEnabled;
        _configuration.MuteEnabled = mute;
        _configuration.Save();
        if (mute)
        {
          _messageDispatcher.ClearQueue();
          _playbackService.ClearQueue();
          _playbackService.StopAll();
        }
        _logger.Chat(mute ? "Muted" : "Unmuted");
        break;
      case "pause":
        bool paused = !_playbackService.Paused;
        _playbackService.Paused = paused;
        _logger.Chat(paused ? "Paused" : "Unpaused");
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
