namespace XivVoices.Services;

public interface IReportService : IHostedService
{
  void Report(XivMessage message);
  void ReportWithReason(XivMessage message, string reason);
}

public class ReportService(ILogger _logger, Configuration _configuration, IDataService _dataService, IGameInteropService _gameInteropService, IClientState _clientState, IDalamudPluginInterface _pluginInterface) : IReportService
{
  private bool _languageWarningThisSession = false;
  private bool _invalidPluginsWarningsThisSession = false;

  private Dictionary<string, XivReport> _reports = [];
  private readonly HttpClient _httpClient = new();
  private CancellationTokenSource? _cts;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _cts = new();
    _dataService.OnDataDirectoryChanged += OnDataDirectoryChanged;

    LoadReports();
    _ = TryUploadReports(_cts.Token);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _cts?.Cancel();
    _cts?.Dispose();
    _cts = null;

    _dataService.OnDataDirectoryChanged -= OnDataDirectoryChanged;

    SaveReports();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void OnDataDirectoryChanged(object? sender, string dataDirectory) =>
    LoadReports();

  private void LoadReports()
  {
    string filePath = Path.Join(_dataService.DataDirectory, "reports.json");
    if (!File.Exists(filePath)) return;

    try
    {
      string jsonContent = File.ReadAllText(filePath);
      Dictionary<string, XivReport> json = JsonSerializer.Deserialize<Dictionary<string, XivReport>>(jsonContent) ?? throw new Exception("Failed to deserialize reports.json");
      _reports = json;
      _logger.Debug($"Loaded {_reports.Count} reports");
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
    }
  }

  private void SaveReports()
  {
    try
    {
      string? dataDirectory = _dataService.DataDirectory;
      if (dataDirectory == null)
      {
        _logger.Debug("DataDirectory not set, can't save reports");
        return;
      }

      string filePath = Path.Join(dataDirectory, "reports.json");
      string json = JsonSerializer.Serialize(_reports, _dataService.JsonWriteOptions);
      File.WriteAllText(filePath, json);
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
    }
  }

  private async Task TryUploadReports(CancellationToken token)
  {
    if (!_dataService.ServerOnline || _reports.Count == 0) return;

    List<string> keysToRemove = [];
    foreach ((string key, XivReport report) in _reports)
    {
      if (token.IsCancellationRequested) break;
      bool success = await TryUploadReport(report, token);
      if (success)
        keysToRemove.Add(key);
    }

    foreach (string key in keysToRemove)
      _reports.Remove(key);

    if (keysToRemove.Count > 0)
      SaveReports();
  }

  private async Task<bool> TryUploadReport(XivReport report, CancellationToken token)
  {
    try
    {
      string json = JsonSerializer.Serialize(report, _dataService.JsonWriteOptions);
      StringContent content = new(json, Encoding.UTF8, "application/json");

      string url = $"{_dataService.ServerUrl}/report";
      using HttpResponseMessage response = await _httpClient.PostAsync(url, content, token);
      response.EnsureSuccessStatusCode();

      _logger.Debug($"Report successfully uploaded: {report.Message.Id}");
      return true;
    }
    catch (HttpRequestException httpEx)
    {
      _logger.Error(httpEx);
      _dataService.ServerOnline = false;
      return false;
    }
    catch (TaskCanceledException)
    {
      _logger.Debug("TryUploadReport was cancelled.");
      return false;
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
      return false;
    }
  }

  private async Task SendOrSaveReport(XivReport report, CancellationToken token)
  {
    bool wasUploaded = false;
    if (_dataService.ServerOnline)
      wasUploaded = await TryUploadReport(report, token);

    if (!wasUploaded)
    {
      _logger.Debug("Saving report to be processed at a later date.");
      _reports[report.Message.Id] = report;
      SaveReports();
    }
  }

  private bool CanReport()
  {
    if (_dataService.DataStatus.UpdateInProgress) return false;

    if (_clientState.ClientLanguage != Dalamud.Game.ClientLanguage.English)
    {
      if (!_languageWarningThisSession)
      {
        _logger.Chat("Unable to report. Your Client's Language is not set to English.");
        _languageWarningThisSession = true;
      }
      return false;
    }

    List<string> invalidPlugins = ["Echoglossian"];
    List<string> loadedPlugins = [];
    foreach (string invalidPlugin in invalidPlugins)
      if (_pluginInterface.InstalledPlugins.Any(pluginInfo => pluginInfo.InternalName == invalidPlugin && pluginInfo.IsLoaded))
        loadedPlugins.Add(invalidPlugin);

    if (loadedPlugins.Count > 0)
    {
      if (!_invalidPluginsWarningsThisSession)
      {
        _logger.Chat($"Unable to report. You have the following unsupported plugins installed: '{string.Join(", ", loadedPlugins)}'");
        _invalidPluginsWarningsThisSession = true;
      }
      return false;
    }

    bool mostVoicelinesDownloaded = _dataService.Manifest != null && (_dataService.DataStatus.VoicelinesDownloaded + 10000) >= _dataService.Manifest.Voicelines.Count;
    if (!mostVoicelinesDownloaded)
    {
      _logger.Chat("You are missing over 10k voicelines. Reporting is unavailable, please update.");
      return false;
    }

    return true;
  }

  public void Report(XivMessage message)
  {
    if (!_configuration.EnableAutomaticReports)
    {
      _logger.Debug("Not reporting message due to automatic reports being turned off.");
      return;
    }

    _gameInteropService.RunOnFrameworkThread(() =>
    {
      if (!CanReport() || !_clientState.IsLoggedIn || _clientState.LocalPlayer == null) return;

      if (_configuration.LogReportsToChat)
        _logger.Chat($"Reporting: {message.Speaker}: {message.Sentence}");

      XivReport report = new(
        _dataService.Version,
        message,
        _gameInteropService.GetLocation(),
        _gameInteropService.IsInCutscene(),
        _gameInteropService.IsInDuty(),
        _gameInteropService.GetActiveQuests(),
        _gameInteropService.GetActiveLeves()
      );

      _ = SendOrSaveReport(report, _cts?.Token ?? default);
    });
  }

  public void ReportWithReason(XivMessage message, string reason)
  {
    if (!CanReport()) return;
    _logger.Chat($"Report submitted with reason: {reason}");

    _ = SendOrSaveReport(new(_dataService.Version, message, reason), _cts?.Token ?? default);
  }
}
