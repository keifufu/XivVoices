using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace XivVoices.Services;

public interface ILogger
{
  Configuration Configuration { get; set; }

  void Toast(string pre, string italic = "", string post = "");
  void Chat(string pre, string italic = "", string post = "");

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

public class Logger(IPluginLog PluginLog, IToastGui ToastGui, IChatGui ChatGui) : ILogger
{
  public Configuration Configuration { get; set; } = new Configuration();

  public void Toast(string pre, string italic = "", string post = "")
  {
    ToastGui.ShowNormal(
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

  public void Chat(string pre, string italic = "", string post = "")
  {
    XivChatEntry chatMessage = new()
    {
      Type = XivChatType.Debug,
      Message = new SeStringBuilder()
        .AddUiForeground("[XivVoices] ", 35)
        .AddText(pre)
        .AddItalics(italic)
        .AddText(post)
        .Build(),
    };
    ChatGui.Print(chatMessage);
    Debug($"Printed chatMessage::'{chatMessage.Message}'");
  }

  private string FormatCallsite(string callerPath = "", string callerName = "", int lineNumber = -1) =>
    $"[{Path.GetFileName(callerPath)}:{callerName}:{lineNumber}]";

  public void Error(string text, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) =>
    PluginLog.Error($"{FormatCallsite(callerPath, callerName, lineNumber)} {text}");

  public void Error(Exception ex, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) =>
    PluginLog.Error($"{FormatCallsite(callerPath, callerName, lineNumber)} Exception: {ex}");

  public void Debug(string text, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1)
  {
    if (!Configuration.DebugLogging) return;
    PluginLog.Debug($"{FormatCallsite(callerPath, callerName, lineNumber)} {text}");
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
