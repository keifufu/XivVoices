namespace XivVoices.Types;

public class XivReport
{
  public ReportType Type { get; }
  public string PluginVersion { get; }
  public string Date { get; }
  public XivMessage Message { get; }

  // These are not added for manual reports, as the player
  // could be in another zone by that point.
  public string? Location { get; }
  public bool? IsInCutscene { get; }
  public bool? IsInDuty { get; }
  public List<string>? ActiveQuests { get; }
  public List<string>? ActiveLeves { get; }

  // Manual report reason
  public string? Reason { get; }

  private string GetPluginVersion() =>
    Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0";

  private string GetDate() =>
    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

  public XivReport(XivMessage message, string location, bool isInCutscene, bool isInDuty, List<string> activeQuests, List<string> activeLeves)
  {
    Type = ReportType.Automatic;
    PluginVersion = GetPluginVersion();
    Date = GetDate();

    Message = message;
    Location = location;
    IsInCutscene = isInCutscene;
    IsInDuty = isInDuty;
    ActiveQuests = activeQuests;
    ActiveLeves = activeLeves;
  }

  public XivReport(XivMessage message, string reason)
  {
    Type = ReportType.Manual;
    PluginVersion = GetPluginVersion();
    Date = GetDate();

    Message = message;
    Reason = reason;
  }

  [JsonConstructor]
  public XivReport(
    ReportType type,
    string pluginVersion,
    string date,
    XivMessage message,
    string? location,
    bool? isInCutscene,
    bool? isInDuty,
    List<string>? activeQuests,
    List<string>? activeLeves,
    string? reason)
  {
    Type = type;
    PluginVersion = pluginVersion;
    Date = date;
    Message = message;
    Location = location;
    IsInCutscene = isInCutscene;
    IsInDuty = isInDuty;
    ActiveQuests = activeQuests;
    ActiveLeves = activeLeves;
    Reason = reason;
  }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportType
{
  Automatic,
  Manual
}
