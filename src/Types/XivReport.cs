namespace XivVoices.Types;

public class XivReport
{
  public ReportType ReportType { get; }
  public string PluginVersion { get; }
  public string Date { get; }
  public XivMessage Message { get; }

  // Player Information
  // These are not added for manual reports, as the player
  // could be in another zone by that point.
  public string? Location { get; }
  public string? Coordinates { get; }
  public bool? IsInCutscene { get; }
  public bool? IsInDuty { get; }
  public List<string>? ActiveQuests { get; }
  public List<string>? ActiveLeves { get; }

  // Manual report reason
  public string? Reason { get; }

  public XivReport(XivMessage message, string location, string coordinates, bool isInCutscene, bool isInDuty, List<string> activeQuests, List<string> activeLeves)
  {
    ReportType = ReportType.Automatic;
    PluginVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0";
    Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); ;

    Message = message;
    Location = location;
    Coordinates = coordinates;
    IsInCutscene = isInCutscene;
    IsInDuty = isInDuty;
    ActiveQuests = activeQuests;
    ActiveLeves = activeLeves;
  }

  public XivReport(XivMessage message, string reason)
  {
    ReportType = ReportType.Manual;
    PluginVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0";
    Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); ;

    Message = message;
    Reason = reason;
  }

  [JsonConstructor]
  public XivReport(
    ReportType reportType,
    string pluginVersion,
    string date,
    XivMessage message,
    string? location,
    string? coordinates,
    bool? isInCutscene,
    bool? isInDuty,
    List<string>? activeQuests,
    List<string>? activeLeves,
    string? reason)
  {
    ReportType = reportType;
    PluginVersion = pluginVersion;
    Date = date;
    Message = message;
    Location = location;
    Coordinates = coordinates;
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
