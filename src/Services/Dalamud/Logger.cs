using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;

namespace XivVoices.Services;

public interface ILogger : IHostedService
{
  List<string> LogHistory { get; }

  void SetConfiguration(Configuration configuration);

  void Toast(string pre, string italic = "", string post = "");
  void Chat(string pre, string italic = "", string post = "", string name = "", XivChatType type = XivChatType.Debug, bool addPrefix = true);

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

  void DebugObj<T>(T obj,
      [CallerFilePath] string callerPath = "",
      [CallerMemberName] string callerName = "",
      [CallerLineNumber] int lineNumber = -1);

  void ServiceLifecycle(string? status = null,
      [CallerFilePath] string callerPath = "",
      [CallerMemberName] string callerName = "",
      [CallerLineNumber] int lineNumber = -1);
}

public class Logger(IPluginLog _pluginLog, IToastGui _toastGui, IChatGui _chatGui) : ILogger
{
  private Configuration _configuration { get; set; } = new Configuration();
  public List<string> LogHistory { get; } = [];

  public Task StartAsync(CancellationToken cancellationToken)
  {
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

    ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

    ServiceLifecycle();
    return Task.CompletedTask;
  }

  public void SetConfiguration(Configuration configuration)
  {
    _configuration = configuration;
  }

  private void AddLogHistory(string logEntry)
  {
    string timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
    LogHistory.Add($"[{timestamp}] {logEntry}");
  }

  private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
  {
    if (!args.Observed && args.Exception.ToString().Contains("XivVoices"))
      AddLogHistory($"Uncaught Exception: {args.Exception}");
  }

  public void Toast(string pre, string italic = "", string post = "")
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

  public void Chat(string pre, string italic = "", string post = "", string name = "", XivChatType type = XivChatType.Debug, bool addPrefix = true)
  {
    XivChatEntry chatMessage = new()
    {
      Type = type,
      Name = new SeStringBuilder().AddText(name).Build(),
      Message = new SeStringBuilder()
        .AddUiForeground(addPrefix ? "[XivVoices] " : "", 35)
        .AddText(pre)
        .AddItalics(italic)
        .AddText(post)
        .Build(),
    };
    _chatGui.Print(chatMessage);
    Debug($"Printed chatMessage::'{chatMessage.Message}'");
  }

  private string FormatCallsite(string callerPath = "", string callerName = "", int lineNumber = -1) =>
    $"[{Path.GetFileName(callerPath)}:{callerName}:{lineNumber}]";

  public void Error(string text, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1)
  {
    string logEntry = $"{FormatCallsite(callerPath, callerName, lineNumber)} {text}";
    _pluginLog.Error(logEntry);
    AddLogHistory(logEntry);
  }

  public void Error(Exception ex, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1)
  {
    string logEntry = $"{FormatCallsite(callerPath, callerName, lineNumber)} Exception: {ex}";
    _pluginLog.Error(logEntry);
    AddLogHistory(logEntry);
  }

  public void Debug(string text, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1)
  {
    string logEntry = $"{FormatCallsite(callerPath, callerName, lineNumber)} {text}";
    if (_configuration.DebugLogging) _pluginLog.Debug(logEntry);
    AddLogHistory(logEntry);
  }

  public void DebugObj<T>(T obj, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1)
  {
    if (obj == null)
    {
      Debug("null", callerPath, callerName, lineNumber);
      return;
    }

    Type type = typeof(T);
    StringBuilder sb = new();
    sb.AppendLine($"Type: {type.Name}");

    PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
    foreach (PropertyInfo prop in properties)
    {
      object? value = prop.GetValue(obj);
      sb.AppendLine($"  {prop.Name}: {value ?? null}");
    }

    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
    foreach (FieldInfo field in fields)
    {
      object? value = field.GetValue(obj);
      sb.AppendLine($"  {field.Name}: {value ?? null}");
    }

    if (properties.Length == 0 && fields.Length == 0)
      sb.AppendLine("  No public properties or fields found.");

    Debug(sb.ToString(), callerPath, callerName, lineNumber);
  }

  public void ServiceLifecycle(string? status = null, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1)
  {
    string lifecycleStage = status ??
      (callerName.Contains("Start")
      ? "started" : callerName.Contains("Stop")
      ? "stopped" : "changed");

    string className = new StackTrace()
      .GetFrame(1)
      ?.GetMethod()
      ?.DeclaringType
      ?.Name ?? "UnknownClass";

    Debug($"{className} {lifecycleStage}", callerPath, callerName, lineNumber);
  }
}
