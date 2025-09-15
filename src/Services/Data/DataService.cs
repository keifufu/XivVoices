namespace XivVoices.Services;

public interface IDataService : IHostedService
{
  bool ServerOnline { get; set; }
  DataStatus DataStatus { get; }
  Manifest? Manifest { get; }
  List<string> AvailableDrives { get; }
  string? DataDirectory { get; }
  string? ToolsDirectory { get; }
  string? VoicelinesDirectory { get; }
  string ServerUrl { get; }
  string Version { get; }
  string LatestVersion { get; }
  event EventHandler<string>? OnDataDirectoryChanged;
  JsonSerializerOptions JsonWriteOptions { get; }
  void SetDataDirectory(string dataDirectory);
  void SetServerUrl(string serverUrl);
  Task Update(bool forceDownloadManifest = false);
  void CancelUpdate();
  string? TempFilePath(string fileName);
  NpcEntry? TryGetCachedPlayer(string speaker);
  void CachePlayer(string speaker, NpcEntry npc);
}

public class DataService(ILogger _logger, Configuration _configuration) : IDataService
{
  private Dictionary<string, NpcEntry> _cachedPlayers = [];
  private readonly HttpClient _httpClient = new();
  private CancellationTokenSource? _cts;
  private readonly SemaphoreSlim _semaphore = new(25);

  public bool ServerOnline { get; set; } = false;
  public DataStatus DataStatus { get; private set; } = new();
  public Manifest? Manifest { get; private set; } = null;

  public event EventHandler<string>? OnDataDirectoryChanged;

  public JsonSerializerOptions JsonWriteOptions { get; } = new()
  {
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true
  };

  private bool _dataDirectoryExists;
  public string? DataDirectory
  {
    get
    {
      if (!_dataDirectoryExists) return null;
      return _configuration.DataDirectory;
    }
  }

  private List<string> _availableDrives = [];
  public List<string> AvailableDrives
  {
    get
    {
      if (_availableDrives.Count > 0) return _availableDrives;
      _availableDrives = DriveInfo.GetDrives()
        .Where(drive => drive.IsReady)
        .Select(drive => drive.Name.Trim('\\'))
        .ToList();
      return _availableDrives;
    }
  }

  private bool _toolsDirectoryExists;
  public string? ToolsDirectory
  {
    get
    {
      string toolsDirectory = Path.Join(DataDirectory, "tools");
      if (_toolsDirectoryExists) return toolsDirectory;
      if (Directory.Exists(toolsDirectory))
      {
        _toolsDirectoryExists = true;
        return toolsDirectory;
      }
      return null;
    }
  }

  private bool _voicelinesDirectoryExists;
  public string? VoicelinesDirectory
  {
    get
    {
      string? dataDirectory = DataDirectory;
      if (dataDirectory == null) return null;
      string voicelinesDirectory = Path.Join(dataDirectory, "voicelines");
      if (_voicelinesDirectoryExists) return voicelinesDirectory;
      if (!Directory.Exists(voicelinesDirectory))
      {
        Directory.CreateDirectory(voicelinesDirectory);
        _voicelinesDirectoryExists = true;
      }
      return voicelinesDirectory;
    }
  }

  public string ServerUrl
  {
    get
    {
      if (string.IsNullOrEmpty(_configuration.ServerUrl))
        return "https://xivv.keifufu.dev";
      return _configuration.ServerUrl;
    }
    private set
    {
      _configuration.ServerUrl = value;
      _configuration.Save();
    }
  }

  public string Version
  {
    get => Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0";
  }

  public string LatestVersion { get; private set; } = "0.0.0.0";

  public string? TempFilePath(string fileName)
  {
    string? dataDirectory = DataDirectory;
    if (dataDirectory == null) return null;
    return Path.Join(dataDirectory, fileName);
  }

  public void SetDataDirectory(string dataDirectory)
  {
    _dataDirectoryExists = Directory.Exists(dataDirectory);
    if (!_dataDirectoryExists)
    {
      Directory.CreateDirectory(dataDirectory);
      _dataDirectoryExists = Directory.Exists(dataDirectory);
    }
    _configuration.DataDirectory = dataDirectory;
    _configuration.Save();

    // Incase the new data directory is an existing installation, try to import these instead of overwriting them with empty ones.
    LoadCachedPlayers();
    OnDataDirectoryChanged?.Invoke(this, dataDirectory); // Used in ReportService for localReports.json

    _ = Update();
  }

  public void SetServerUrl(string serverUrl)
  {
    ServerUrl = serverUrl;
    _ = UpdateServerStatus(default);
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _dataDirectoryExists = Directory.Exists(_configuration.DataDirectory);
    _ = Update();
    LoadCachedPlayers();

    _logger.Debug($"XivVoices v{Version}");

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    CancelUpdate();
    SaveCachedPlayers();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private async Task SetLatestVersion()
  {
    if (!ServerOnline)
    {
      _logger.Debug("Server is not online, cannot check for latest plugin version.");
      LatestVersion = Version;
      return;
    }

    HttpResponseMessage response = await _httpClient.GetAsync($"{ServerUrl}/repo");
    if (response.StatusCode == System.Net.HttpStatusCode.OK)
    {
      string jsonResponse = await response.Content.ReadAsStringAsync();
      using JsonDocument doc = JsonDocument.Parse(jsonResponse);

      if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
      {
        string assemblyVersion = doc.RootElement[0].GetProperty("AssemblyVersion").GetString() ?? "0.0.0.0";
        LatestVersion = Version == "0.0.0.0" ? Version : assemblyVersion;
      }
    }
    else
    {
      _logger.Debug($"Failed to retrieve latest plugin version with code: {response.StatusCode}");
      LatestVersion = Version;
    }
  }

  private async Task UpdateServerStatus(CancellationToken token)
  {
    try
    {
      string statusEndpoint = $"{ServerUrl}/_status";
      HttpResponseMessage response = await _httpClient.GetAsync(statusEndpoint, token);

      if (response.IsSuccessStatusCode)
      {
        string content = await response.Content.ReadAsStringAsync(token);
        if (content.Trim().Equals("xivv", StringComparison.OrdinalIgnoreCase))
        {
          _logger.Debug("Server is online");
          ServerOnline = true;
        }
        else
        {
          _logger.Debug($"'{statusEndpoint}' returned invalid response '{content}'");
        }
      }
    }
    catch (OperationCanceledException)
    {
      _logger.Debug("UpdateServerStatus was cancelled");
    }
    catch
    {
      _logger.Debug($"UpdateServerStatus failed to reach server: {ServerUrl}");
      ServerOnline = false;
    }

    _ = SetLatestVersion();
  }

  private async Task LoadManifest(bool forceDownload, CancellationToken token)
  {
    string? dataDirectory = DataDirectory;
    if (dataDirectory == null)
    {
      _logger.Debug("Can't load manifest: DataDirectory doesn't exist.");
      return;
    }

    string manifestPath = Path.Join(DataDirectory, "manifest.json");
    bool manifestExists = File.Exists(manifestPath);
    bool shouldDownload = forceDownload || !manifestExists;
    if (manifestExists)
    {
      DateTime lastModified = File.GetLastWriteTime(manifestPath);
      if (DateTime.Now - lastModified > TimeSpan.FromHours(24))
      {
        _logger.Debug("Manifest file is older than 24 hours, redownloading.");
        shouldDownload = true;
      }
    }

    if (shouldDownload && ServerOnline)
      await DownloadFile(manifestPath, "manifest.json", token);

    // If no new manifest was downloaded and it was already loaded, don't load it again.
    if (!shouldDownload && Manifest != null) return;

    try
    {
      string jsonContent = File.ReadAllText(manifestPath);
      ManifestJson json = JsonSerializer.Deserialize<ManifestJson>(jsonContent) ?? throw new Exception("Failed to deserialize manifest.json");

      Manifest manifest = new()
      {
        ToolsMd5 = json.ToolsMd5,
        Voices = [],
        Npcs = [],
        Npcs_Generic = [],
        Voicelines = json.Voicelines,
        IgnoredSpeakers = json.IgnoredSpeakers,
        SpeakerMappings = [],
        Lexicon = []
      };

      foreach (VoiceEntry entry in json.Voices)
      {
        manifest.Voices[entry.Id] = entry;
      }

      foreach (NpcEntry entry in json.Npcs)
      {
        if (entry.Id != null)
        {
          manifest.Npcs[entry.Id] = entry;
          foreach (string speaker in entry.Speakers)
          {
            manifest.Npcs[speaker] = entry;
          }
        }
      }

      foreach (NpcEntry entry in json.Npcs)
      {
        if (entry.VoiceId == null) continue;
        if (!manifest.Voices.TryGetValue(entry.VoiceId, out VoiceEntry? voice)) continue;
        if (!voice.IsGeneric) continue;

        if (entry.Body == "Beastman")
        {
          string key1 = entry.Race;
          if (!manifest.Npcs_Generic.ContainsKey(key1))
          {
            manifest.Npcs_Generic[key1] = entry;
          }

          string key2 = entry.Race + entry.Gender;
          if (!manifest.Npcs_Generic.ContainsKey(key2))
            manifest.Npcs_Generic[key2] = entry;
        }
        else
        {
          string key = entry.Gender + entry.Race + entry.Tribe + entry.Body + entry.Eyes;
          if (!manifest.Npcs_Generic.ContainsKey(key))
            manifest.Npcs_Generic[key] = entry;
        }
      }

      foreach (SpeakerMappingEntry entry in json.SpeakerMappings)
      {
        if (!manifest.SpeakerMappings.ContainsKey(entry.Type))
          manifest.SpeakerMappings[entry.Type] = [];
        string sanitizedSentence = Regex.Replace(entry.Sentence, @"[^a-zA-Z]", "");
        manifest.SpeakerMappings[entry.Type][sanitizedSentence] = entry.NpcId;
      }

      Manifest = manifest;
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
      return;
    }
  }

  public Task Update(bool forceDownloadManifest = false) => Task.Run(() => UpdateInternal(forceDownloadManifest));

  private async Task UpdateInternal(bool forceDownloadManifest)
  {
    await Task.Delay(1); // hopefully get us off the main thread

    if (_cts != null)
    {
      _cts.Cancel();
      _cts.Dispose();
    }

    _cts = new CancellationTokenSource();
    CancellationToken token = _cts.Token;
    DataStatus.UpdateInProgress = true;

    await UpdateServerStatus(token);

    // We want to load the manifest even if the server is offline.
    await LoadManifest(forceDownloadManifest, token);

    string? dataDirectory = DataDirectory;
    if (dataDirectory == null)
    {
      _logger.Debug("DataDirectory not found, can't update.");
      DataStatus.UpdateInProgress = false;
      return;
    }

    string voicelinesDirectory = VoicelinesDirectory!; // We know that DataDirectory exists, we check right above this.
    DataStatus.VoicelinesDownloaded = new DirectoryInfo(voicelinesDirectory).GetFiles("*", SearchOption.TopDirectoryOnly).Length;

    if (Manifest == null)
    {
      _logger.Debug("Manifest not loaded, can't update.");
      DataStatus.UpdateInProgress = false;
      return;
    }

    DataStatus.UpdateTotalFiles = Manifest.Voicelines.Count;
    DataStatus.UpdateSkippedFiles = 0;
    DataStatus.UpdateCompletedFiles = 0;

    if (!ServerOnline)
    {
      _logger.Debug("Server is not online, can't update.");
      DataStatus.UpdateInProgress = false;
      return;
    }

    string toolsMd5Path = Path.Join(dataDirectory, "tools.md5");
    if (!File.Exists(toolsMd5Path) || File.ReadAllText(toolsMd5Path) != Manifest.ToolsMd5)
    {
      string toolsPath = Path.Join(dataDirectory, "tools");
      if (Directory.Exists(toolsPath))
      {
        _toolsDirectoryExists = false;
        Directory.Delete(toolsPath, recursive: true);
      }

      string zipPath = Path.Join(dataDirectory, "tools.zip");
      await DownloadFile(zipPath, "tools.zip", token);

      if (File.Exists(zipPath))
      {
        // Note: tools.zip is expected NOT to have a subdirectory.
        ZipFile.ExtractToDirectory(zipPath, toolsPath);
        File.Delete(zipPath);
        File.WriteAllText(toolsMd5Path, Manifest.ToolsMd5);
        _logger.Debug("Successfully downloaded tools");
      }
      else
      {
        _logger.Error("Failed to download tools.zip");
      }
    }

    List<(string filePath, string fileName)> missingFiles = [];
    Dictionary<string, long> fileSizeMap = new DirectoryInfo(voicelinesDirectory).GetFiles("*", SearchOption.TopDirectoryOnly).ToDictionary(f => f.Name, f => f.Length);

    foreach (KeyValuePair<string, long> voiceline in Manifest.Voicelines)
    {
      if (token.IsCancellationRequested) break;
      string filePath = Path.Join(voicelinesDirectory, voiceline.Key);
      if (!fileSizeMap.TryGetValue(voiceline.Key, out long size) || size != voiceline.Value)
        missingFiles.Add((filePath, voiceline.Key));
      else
        Interlocked.Increment(ref DataStatus.UpdateSkippedFiles);
    }

    foreach (string file in fileSizeMap.Keys)
    {
      if (!Manifest.Voicelines.ContainsKey(file))
      {
        try
        {
          string filePath = Path.Join(voicelinesDirectory, file);
          _logger.Debug($"Deleting unknown file: {file}");
          File.Delete(filePath);
        }
        catch (Exception ex)
        {
          _logger.Error(ex);
        }
      }
    }

    DataStatus.VoicelinesDownloaded = new DirectoryInfo(voicelinesDirectory).GetFiles("*", SearchOption.TopDirectoryOnly).Length;

    _logger.Debug($"{missingFiles.Count} files need to be updated");
    if (missingFiles.Count == 0)
    {
      DataStatus.UpdateInProgress = false;
      return;
    }

    List<Task> tasks = [];
    DataStatus.UpdateStartTime = DateTime.UtcNow;

    foreach ((string filePath, string fileName) in missingFiles)
    {
      try
      {
        await _semaphore.WaitAsync(token);
      }
      catch (OperationCanceledException)
      {
        _logger.Debug("Cancellation requested during semaphore wait, breaking loop.");
        break;
      }

      if (!ServerOnline)
      {
        _logger.Debug("Server went offline during Update loop, breaking.");
        break;
      }

      tasks.Add(Task.Run(async () =>
      {
        try
        {
          await DownloadFile(filePath, fileName, token);
          Interlocked.Increment(ref DataStatus.UpdateCompletedFiles);
          Interlocked.Increment(ref DataStatus.VoicelinesDownloaded);
        }
        finally
        {
          _semaphore.Release();
        }
      }, token));
    }

    try
    {
      await Task.WhenAll(tasks);
    }
    finally
    {
      DataStatus.UpdateInProgress = false;
      _logger.Debug("Update completed.");
    }
  }

  public void CancelUpdate()
  {
    if (_cts != null)
    {
      _cts.Cancel();
      _cts.Dispose();
      _cts = null;
    }
  }

  private int _subsequentFails = 0;
  private int _cacheHits = 0;
  private int _cacheMisses = 0;
  private int _downloaded = 0;
  private async Task DownloadFile(string filePath, string fileName, CancellationToken token)
  {
    try
    {
      string url = $"{ServerUrl}/files/{fileName}";
      using HttpResponseMessage response = await _httpClient.GetAsync(url, token);
      response.EnsureSuccessStatusCode();
      byte[] fileBytes = await response.Content.ReadAsByteArrayAsync(token);
      await File.WriteAllBytesAsync(filePath, fileBytes, token);

      bool cacheHit = false;
      if (response.Headers.TryGetValues("cf-cache-status", out IEnumerable<string>? values))
      {
        string? cfCacheStatus = values.FirstOrDefault();
        if (cfCacheStatus == "HIT") cacheHit = true;
      }

      if (cacheHit) _cacheHits++;
      else _cacheMisses++;

      if (++_downloaded >= 500)
      {
        _logger.Debug($"Cache Hits: {_cacheHits} | Cache Misses: {_cacheMisses}");
        _downloaded = 0;
        _cacheHits = 0;
        _cacheMisses = 0;
      }

      _subsequentFails = 0;
    }
    catch (HttpRequestException httpEx)
    {
      _subsequentFails++;
      // Not that critical if a voiceline failed, i guess.
      if (_subsequentFails > 15 || !fileName.Contains(".ogg"))
      {
        _logger.Error(httpEx);
        ServerOnline = false;
      }
    }
    catch (OperationCanceledException)
    {
      _logger.Debug($"DownloadFile was cancelled: {fileName}");
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
    }
  }

  private void LoadCachedPlayers()
  {
    string filePath = Path.Join(DataDirectory, "players.json");
    if (!File.Exists(filePath)) return;

    try
    {
      string jsonContent = File.ReadAllText(filePath);
      Dictionary<string, NpcEntry> json = JsonSerializer.Deserialize<Dictionary<string, NpcEntry>>(jsonContent) ?? throw new Exception("Failed to deserialize players.json");
      _cachedPlayers = json;
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
    }
  }

  private void SaveCachedPlayers()
  {
    try
    {
      string? dataDirectory = DataDirectory;
      if (dataDirectory == null)
      {
        _logger.Debug("Can't save players.json. No DataDirectory found.");
        return;
      }

      string filePath = Path.Join(dataDirectory, "players.json");
      string json = JsonSerializer.Serialize(_cachedPlayers, JsonWriteOptions);
      File.WriteAllText(filePath, json);
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
    }
  }

  public NpcEntry? TryGetCachedPlayer(string speaker)
  {
    if (_cachedPlayers.TryGetValue(speaker, out NpcEntry? npcEntry))
      return npcEntry;
    return null;
  }

  public void CachePlayer(string speaker, NpcEntry npc)
  {
    _cachedPlayers[speaker] = npc;
    SaveCachedPlayers();
  }
}
