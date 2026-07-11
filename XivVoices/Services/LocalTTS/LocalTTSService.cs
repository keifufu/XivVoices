using System.Security.Cryptography;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using Microsoft.ML.OnnxRuntime;
using FrameworkStruct = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace XivVoices.Services;

public interface ILocalTTSService : IHostedService
{
  List<LocalTTSVoice> Voices { get; }
  Task<(WaveStream? waveStream, int relativeVolume)> Generate(XivMessage message);
  void Reinitialize();
  int ResolvePitch(XivMessage message);
  event System.Action? OnInitialized;
}

public partial class LocalTTSService(ILogger _logger, Configuration _configuration, IDataService _dataService, IGameInteropService _gameInteropService, IGameConfig _gameConfig, IDalamudPluginInterface _pluginInterface) : ILocalTTSService
{
  public event System.Action? OnInitialized;
  public List<LocalTTSVoice> Voices { get; private set; } = [];

  private readonly KokoroTTSPipelineConfig _pipelineConfig = new(new DefaultSegmentationConfig()
  {
    MaxFirstSegmentLength = 510,
    MaxSecondSegmentLength = 510,
  });
  private bool _initialized;
  private SessionOptions? _sessionOptions;
  private KokoroModel? _model;

  public Task StartAsync(CancellationToken token)
  {
    OrtEnv.DisableDllImportResolver = true;
    NativeLibrary.SetDllImportResolver(Assembly.Load("Microsoft.ML.OnnxRuntime"), DllImportResolver);
    NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

    if (_dataService.ToolsDirectory != null && IsToolsReady()) Initialize(_dataService.ToolsDirectory);
    _dataService.OnToolsDownloaded += Reinitialize;
    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken token)
  {
    _dataService.OnToolsDownloaded -= Reinitialize;
    return _logger.ServiceLifecycle();
  }

  private IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
  {
    try
    {
      string? assemblyLocation = _pluginInterface.AssemblyLocation.Directory?.FullName;
      if (string.IsNullOrEmpty(assemblyLocation)) return IntPtr.Zero;

      string? fileName = libraryName switch
      {
        "espeak-ng" => "espeak-ng.dll",
        "onnxruntime" => "onnxruntime.dll",
        _ => null,
      };

      return fileName is null ? IntPtr.Zero : NativeLibrary.Load(Path.Join(assemblyLocation, fileName));
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
      return IntPtr.Zero;
    }
  }

  private bool IsToolsReady()
  {
    if (_dataService.ToolsDirectory == null) return false;
    if (!File.Exists(Path.Join(_dataService.ToolsDirectory, "kokoro-quant.onnx"))) return false;
    if (!Directory.Exists(Path.Join(_dataService.ToolsDirectory, "voices"))) return false;

    // espeak-ng crashes without these at the minimum.
    // just make sure they exist to prevent crashes after a corrupt tools update
    if (!File.Exists(Path.Join(_dataService.ToolsDirectory, "espeak-ng-data", "intonations"))) return false;
    if (!File.Exists(Path.Join(_dataService.ToolsDirectory, "espeak-ng-data", "phondata"))) return false;
    if (!File.Exists(Path.Join(_dataService.ToolsDirectory, "espeak-ng-data", "phonindex"))) return false;
    if (!File.Exists(Path.Join(_dataService.ToolsDirectory, "espeak-ng-data", "phontab"))) return false;

    return true;
  }

  public void Reinitialize()
  {
    if (_dataService.ToolsDirectory == null || !IsToolsReady()) return;
    _logger.Debug("Reinitializing LocalTTS");
    Dispose();
    Initialize(_dataService.ToolsDirectory);
  }

  private void Initialize(string toolsDirectory)
  {
    if (_initialized) return;
    _logger.Debug("Initializing LocalTTS");
    try
    {
      _sessionOptions ??= new() { IntraOpNumThreads = _configuration.LocalTTSThreads, InterOpNumThreads = 1 };
      _model ??= new KokoroModel(Path.Join(toolsDirectory, "kokoro-quant.onnx"), _sessionOptions);
      foreach (string filePath in Directory.GetFiles(Path.Join(toolsDirectory, "/voices")).Where(f => f.EndsWith(".npy")))
        Voices.Add(LocalTTSVoice.FromPath(filePath));
      InitializePhonemizer(toolsDirectory);
      InitializeTokenizer();
      _initialized = true;
      OnInitialized?.Invoke();
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
    }
  }

  private void Dispose()
  {
    if (!_initialized) return;
    _logger.Debug("Disposing LocalTTS");
    _model?.Dispose();
    _model = null;
    _sessionOptions?.Dispose();
    _sessionOptions = null;
    Voices.Clear();
    DisposePhonemizer();
    _initialized = false;
  }

  public Task<(WaveStream? waveStream, int relativeVolume)> Generate(XivMessage message) => Task.Run(() => Generate_Internal(message));

  public int ResolvePitch(XivMessage message)
  {
    if (message.PitchOverride != null) return message.PitchOverride.Value;

    string speaker = message.Source == MessageSource.ChatMessage || message.Source == MessageSource.SelectString ? $"{message.Speaker}@{message.SpeakerWorld}" : message.Speaker;
    if (_configuration.LocalTTSOverrides.TryGetValue(speaker, out (string voice, int pitch) options)) return options.pitch;

    if (_configuration.LocalTTSPitchRandomization)
    {
      byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(speaker));
      ulong v = BitConverter.ToUInt64(hash);
      const int min = 90, max = 110;
      int range = max - min + 1;
      return (int)(min + (v % (ulong)range));
    }

    return 100;
  }

  private LocalTTSVoice? ResolveVoice(XivMessage message)
  {
    if (!_initialized) return null;

    if (message.VoiceOverride != null)
    {
      LocalTTSVoice? match = Voices.FirstOrDefault(v => v.Name == message.VoiceOverride);
      if (match != null) return match;
    }

    string speaker = message.Source == MessageSource.ChatMessage || message.Source == MessageSource.SelectString ? $"{message.Speaker}@{message.SpeakerWorld}" : message.Speaker;
    if (_configuration.LocalTTSOverrides.TryGetValue(speaker, out (string voice, int pitch) options))
    {
      LocalTTSVoice? match = Voices.FirstOrDefault(v => v.Name == options.voice);
      if (match != null) return match;
    }

    string gender = message.Npc?.Gender ?? _configuration.LocalTTSDefaultVoice;
    string defaultVoice = gender == "Male" ? _configuration.LocalTTSMaleVoice : _configuration.LocalTTSFemaleVoice;

    if (message.Source == MessageSource.ChatMessage && _configuration.LocalTTSChatChannelVoicesEnabled)
    {
      if (_configuration.LocalTTSChatChannelVoices.TryGetValue(message.ChatChannel ?? XivChatType.Say, out (string? male, string? female) chatChannelVoices))
      {
        string? voice = (gender == "Male" ? chatChannelVoices.male : chatChannelVoices.female) ?? defaultVoice;
        LocalTTSVoice? match = Voices.FirstOrDefault(v => v.Name == voice);
        if (match != null) return match;
      }
      // If we didn't find a match, we fall back to the non-randomized default voices on purpose.
    }
    else if (_configuration.LocalTTSVoiceRandomization)
    {
      List<LocalTTSVoice> canonical = Voices.OrderBy(v => v.Name).ToList();
      byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(speaker));
      ulong v = BitConverter.ToUInt64(hash);
      int start = (int)(v % (ulong)canonical.Count);

      for (int i = 0; i < canonical.Count; i++)
      {
        LocalTTSVoice candidate = canonical[(start + i) % canonical.Count];
        if (!_configuration.LocalTTSDisallowedVoices.Contains(candidate.Name) && candidate.Gender == gender)
          return candidate;
      }

      _logger.Debug($"Failed to find randomized voice for '{speaker}', falling back to default voice.");
    }

    return Voices.FirstOrDefault(v => v.Name == defaultVoice);
  }

  private int GetRelativeVolume(LocalTTSVoice voice)
  {
    return voice.Name switch
    {
      // Male Voices
      "Adam" => 0,
      "Daniel" => 50,
      "Echo" => 30,
      "Eric" => 50,
      "Fable" => 40,
      "Fenrir" => 0,
      "George" => 40,
      "Lewis" => 10,
      "Liam" => 20,
      "Michael" => 50,
      "Onyx" => 60,
      "Puck" => -10,
      // Female Voices
      "Alice" => 30,
      "Alloy" => 50,
      "Aoede" => 20,
      "Bella" => 30,
      "Emma" => 0,
      "Heart" => 40,
      "Isabella" => -10,
      "Jessica" => 30,
      "Kore" => 10,
      "Lily" => 20,
      "Nicole" => 10,
      "Nova" => 140,
      "River" => 0,
      "Sarah" => -20,
      "Sky" => 40,
      _ => 0,
    };
  }

  private async Task<T> WithLimitedFPS<T>(Func<Task<T>> func, bool enabled)
  {
    if (!enabled)
      return await func();

    bool fpsInActiveBefore = _gameConfig.System.GetBool("FPSInActive");
    if (!fpsInActiveBefore) _gameConfig.System.Set("FPSInActive", true);
    unsafe { FrameworkStruct.Instance()->WindowInactive = true; }

    try
    {
      return await func();
    }
    finally
    {
      if (!fpsInActiveBefore) _gameConfig.System.Set("FPSInActive", fpsInActiveBefore);
      unsafe { FrameworkStruct.Instance()->WindowInactive = false; }
    }
  }

  private async Task<(WaveStream? waveStream, int relativeVolume)> Generate_Internal(XivMessage message)
  {
    LocalTTSVoice? voice = ResolveVoice(message);

    string finalMessage = ApplyLexicon(message);
    if (finalMessage.IsNullOrWhitespace()) return (null, 0);

    if (_configuration.LocalTTSRemoteEnabled)
    {
      try
      {
        WaveStream? waveStream = await WithLimitedFPS(async () =>
        {
          string requestUri = _configuration.LocalTTSRemoteUri
            .Replace("%n", Uri.EscapeDataString(message.Npc?.Id ?? "null"))
            .Replace("%v", Uri.EscapeDataString(message.Voice?.Id ?? "null"))
            .Replace("%s", Uri.EscapeDataString(message.Speaker))
            .Replace("%t", Uri.EscapeDataString(finalMessage))
            .Replace("%k", Uri.EscapeDataString(voice?.Name ?? "null"));

          HttpResponseMessage response = await _dataService.HttpClient.GetAsync(requestUri, message.GenerationToken.Token);
          if (!response.IsSuccessStatusCode)
          {
            _logger.Debug($"Remote LocalTTS failed with code: {response.StatusCode}");
            return null;
          }

          byte[] bytes = await response.Content.ReadAsByteArrayAsync();
          MemoryStream memoryStream = new(bytes);
          return new WaveFileReader(memoryStream);
        }, _configuration.LocalTTSRemoteFPSLimit);

        if (waveStream != null) return (waveStream, 0);
      }
      catch (OperationCanceledException)
      {
        _logger.Debug("LocalTTS was cancelled");
        return (null, 0);
      }
      catch (Exception ex)
      {
        _logger.Error(ex);
      }

      _logger.Debug("Falling back to actual LocalTTS");
    }

    if (!_initialized)
    {
      _logger.Debug("Not generating LocalTTS: not initialized.");
      return (null, 0);
    }
    Stopwatch sw = Stopwatch.StartNew();

    if (voice == null)
    {
      _logger.Error("Failed to resolve LocalTTS voice");
      return (null, 0);
    }
    _logger.Debug($"Using LocalTTS Voice: {voice.Name}");

    try
    {
      int[] tokens = Tokenize(finalMessage);
      List<int[]> segments = _pipelineConfig.SegmentationFunc(tokens);
      KokoroJob job = KokoroJob.Create(segments, voice, _pipelineConfig.Speed, null);

      List<byte> pcm = [];
      List<char>? phonemesCache = segments.Count > 1 ? [] : null;
      foreach (KokoroJob.KokoroJobStep? step in job.Steps)
      {
        step.OnStepComplete = (samples) =>
        {
          pcm.AddRange(KokoroPlayback.GetBytes(KokoroPlayback.PostProcessSamples(samples)));
          if (!_punctuationTokens.Contains(step.Tokens[^1])) { return; }
          float secondsToWait = _pipelineConfig.SecondsOfPauseBetweenProperSegments[_tokenToChar[step.Tokens[^1]]];
          pcm.AddRange(KokoroPlayback.GetBytes(new float[(int)(secondsToWait * KokoroPlayback.waveFormat.SampleRate)]));
        };
      }
      while (!job.isDone) { job.Progress(_model); }

      _logger.Debug($"LocalTTS took {sw.ElapsedMilliseconds}ms");
      message.LocalTTSVoice = voice.Name;

      MemoryStream ms = new();
      using (RawSourceWaveStream reader = new(new MemoryStream(pcm.ToArray()), new WaveFormat(24000, 16, 1)))
        WaveFileWriter.WriteWavFileToStream(ms, reader);
      ms.Position = 0;
      return (new WaveFileReader(ms), GetRelativeVolume(voice));
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
      _logger.Debug(finalMessage);
      return (null, 0);
    }
  }
}
