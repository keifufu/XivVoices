using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;

namespace XivVoices.Services;

public interface ILogger : IHostedService
{
  List<string> LogHistory { get; }

  void SetConfiguration(Configuration configuration);

  void DalamudToast(NotificationType type, string title, string text, int durationSeconds = 5);

  void Toast(string pre = "", string italic = "", string post = "");

  void Chat(
    string uncolored = "",
    string pre = "",
    string italic = "",
    string post = "",
    string name = "",
    XivChatType? channel = null,
    bool addPrefix = true,
    ushort preColor = 2,
    ushort italicColor = 2,
    ushort postColor = 2);

  void Error(string text,
      [CallerFilePath] string callerPath = "",
      [CallerMemberName] string callerName = "",
      [CallerLineNumber] int lineNumber = -1);

  void Error(Exception ex,
      [CallerFilePath] string callerPath = "",
      [CallerMemberName] string callerName = "",
      [CallerLineNumber] int lineNumber = -1);

  void Debug(string text,
      [CallerFilePath] string callerPath = "",
      [CallerMemberName] string callerName = "",
      [CallerLineNumber] int lineNumber = -1);

  Task ServiceLifecycle(string? status = null,
      [CallerFilePath] string callerPath = "",
      [CallerMemberName] string callerName = "",
      [CallerLineNumber] int lineNumber = -1);
}

public class Logger(
  IChatGui _chatGui,
  IToastGui _toastGui,
  IPluginLog _pluginLog,
  INotificationManager _notificationManager) : ILogger
{
  private Configuration _configuration = new();
  public List<string> LogHistory { get; } = [];

  public Task StartAsync(CancellationToken cancellationToken)
  {
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

    return ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

    return ServiceLifecycle();
  }

  public void SetConfiguration(Configuration configuration)
    => _configuration = configuration;

  // if (string.IsNullOrEmpty(_configuration.StreamElementsApiKey)) return log;
  // return log.Replace(_configuration.StreamElementsApiKey, "REDACTED");
  private string SanitizeLog(string log) => log;

  private void AddLogHistory(string logEntry)
  {
    string timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    LogHistory.Add($"[{timestamp}] {logEntry}");
  }

  private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
  {
    if (!args.Observed && args.Exception.ToString().Contains("XivVoices"))
      AddLogHistory($"Uncaught Exception: {args.Exception}");
  }

  public void DalamudToast(NotificationType type, string title, string text, int durationSeconds = 5)
  {
    _notificationManager.AddNotification(new()
    {
      Content = text,
      Title = title,
      Type = type,
      Minimized = false,
      InitialDuration = TimeSpan.FromSeconds(durationSeconds)
    });
  }

  public void Toast(string pre = "", string italic = "", string post = "")
  {
    _toastGui.ShowNormal(
      new SeStringBuilder()
        .AddText(pre)
        .AddItalics(italic)
        .AddText(post)
        .Build(),
      new ToastOptions
      {
        Position = ToastPosition.Bottom,
        Speed = ToastSpeed.Fast,
      }
    );
  }

  public void Chat(
    string uncolored = "",
    string pre = "",
    string italic = "",
    string post = "",
    string name = "",
    XivChatType? channel = null,
    bool addPrefix = true,
    ushort preColor = 2,
    ushort italicColor = 2,
    ushort postColor = 2)
  {
    XivChatEntry chatMessage = new()
    {
      Type = channel ?? _configuration.DefaultChatChannel,
      Name = new SeStringBuilder().AddText(name).Build(),
      Message = new SeStringBuilder()
        .AddUiForeground(addPrefix ? "[XivVoices] " : "", 35)
        .AddText(channel != null ? uncolored : "")
        .AddUiForeground(channel == null ? uncolored : "", 2)
        .AddUiForeground(pre, preColor)
        .AddItalicsOn()
        .AddUiForeground(italic, italicColor)
        .AddItalicsOff()
        .AddUiForeground(post, postColor)
        .Build(),
    };
    _chatGui.Print(chatMessage);
    Debug($"Printed chatMessage::'{chatMessage.Message}'");
  }

  private string FormatCallsite(string callerPath = "", string callerName = "", int lineNumber = -1) =>
    $"[{Path.GetFileName(callerPath)}:{callerName}:{lineNumber}]";

  public void Error(string text,
    [CallerFilePath] string callerPath = "",
    [CallerMemberName] string callerName = "",
    [CallerLineNumber] int lineNumber = -1)
  {
    string logEntry = SanitizeLog($"{FormatCallsite(callerPath, callerName, lineNumber)} {text}");
    _pluginLog.Error(logEntry);
    AddLogHistory(logEntry);
  }

  public void Error(Exception ex,
    [CallerFilePath] string callerPath = "",
    [CallerMemberName] string callerName = "",
    [CallerLineNumber] int lineNumber = -1)
  {
    string logEntry = SanitizeLog($"{FormatCallsite(callerPath, callerName, lineNumber)} Exception: {ex}");
    _pluginLog.Error(logEntry);
    AddLogHistory(logEntry);
  }

  public void Debug(string text,
    [CallerFilePath] string callerPath = "",
    [CallerMemberName] string callerName = "",
    [CallerLineNumber] int lineNumber = -1)
  {
    string logEntry = SanitizeLog($"{FormatCallsite(callerPath, callerName, lineNumber)} {text}");
    if (_configuration.DebugLogging) _pluginLog.Debug(logEntry);
    AddLogHistory(logEntry);
  }

  public Task ServiceLifecycle(string? status = null,
    [CallerFilePath] string callerPath = "",
    [CallerMemberName] string callerName = "",
    [CallerLineNumber] int lineNumber = -1)
  {
    string className = Path.GetFileNameWithoutExtension(callerPath);
    string lifecycleStage = status ??
      (callerName.Contains("Start")
      ? "started" : callerName.Contains("Stop")
      ? "stopped" : "changed");

    Debug($"{className} {lifecycleStage}", callerPath, callerName, lineNumber);
    return Task.CompletedTask;
  }
}
