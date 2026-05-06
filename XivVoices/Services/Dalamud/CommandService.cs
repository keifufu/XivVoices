using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace XivVoices.Services;

public interface ICommandService : IHostedService;

public class CommandService(ILogger _logger, Configuration _configuration, IWindowService _windowService, IDataService _dataService, IPlaybackService _playbackService, IMessageDispatcher _messageDispatcher, IGameInteropService _gameInteropService, ICommandManager _commandManager) : ICommandService
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

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _commandManager.RemoveHandler(XivVoicesCommand);
    _commandManager.RemoveHandler(XivVoicesCommandAlias);

    return _logger.ServiceLifecycle();
  }

  private async void OnCommand(string command, string arguments)
  {
    _logger.Debug($"command::'{command}' arguments::'{arguments}'");

    string[] args = arguments.Split(" ", StringSplitOptions.RemoveEmptyEntries);
    if (args.Length == 0)
    {
      _windowService.Toggle();
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
        _logger.Chat($"  {command} overlay - Open the overlay window");
        _logger.Chat($"  {command} settings - Open the settings window");
        _logger.Chat($"  {command} overview - Open the overview tab");
        _logger.Chat($"  {command} dialogue - Open the dialogue settings tab");
        _logger.Chat($"  {command} overlaycfg - Open the overlay settings tab");
        _logger.Chat($"  {command} playback - Open the playback settings tab");
        _logger.Chat($"  {command} logs - Open the audio logs tab");
        if (Util.IsWine())
          _logger.Chat($"  {command} wine - Open the wine settings tab");
        if (_configuration.DebugMode)
        {
          _logger.Chat($"  {command} debug - Open the debug tab");
          _logger.Chat($"  {command} selftest - Open the self-test tab");
          _logger.Chat($"  {command} dispatch <speaker>:<sentence> - Dispatch a message");
          _logger.Chat($"  {command} target-info - Prints debug info about the current target");
        }
        _logger.Chat($"  {command}");
        break;
      case "dispatch":
        if (!_configuration.DebugMode) break;
        string joinedString = string.Join(" ", args.Skip(1));
        string[] parts = joinedString.Split([':'], 2);
        string speaker = parts.Length > 0 ? parts[0] : string.Empty;
        string sentence = parts.Length > 1 ? parts[1] : string.Empty;
        _ = _messageDispatcher.TryDispatch(MessageSource.AddonTalk, speaker, sentence, null, true);
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

          (string race, string gender) = _gameInteropService.GetBeastmanRace(target);
          _logger.Chat($"{race} | {gender}");
        }
        break;
      case "version":
        _logger.Chat(pre: $"v{_dataService.Version}", post: $" / v{_dataService.LatestVersion}", preColor: (ushort)(_dataService.IsOutdated ? 15 : 2));
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
      case "overlay":
        _configuration.OverlayOpen = !_configuration.OverlayOpen;
        _configuration.Save();
        break;
      case "settings":
        _windowService.Toggle();
        break;
      case "overview":
        _windowService.OpenTab(ConfigTab.Overview);
        break;
      case "dialogue":
        _windowService.OpenTab(ConfigTab.DialogueSettings);
        break;
      case "overlaycfg":
        _windowService.OpenTab(ConfigTab.OverlaySettings);
        break;
      case "playback":
        _windowService.OpenTab(ConfigTab.PlaybackSettings);
        break;
      case "logs":
        _windowService.OpenTab(ConfigTab.AudioLogs);
        break;
      case "wine":
        _windowService.OpenTab(ConfigTab.WineSettings);
        break;
      case "debug":
        _windowService.OpenTab(ConfigTab.Debug);
        break;
      case "selftest":
        _windowService.OpenTab(ConfigTab.SelfTest);
        break;
      default:
        _logger.Chat("Invalid command:");
        _logger.Chat($"  {command} {arguments}");
        goto case "help";
    }
  }
}
