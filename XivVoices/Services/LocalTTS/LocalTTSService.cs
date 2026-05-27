using System.Security.Cryptography;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;

namespace XivVoices.Services;

public interface ILocalTTSService : IHostedService
{
  List<LocalTTSVoice> Voices { get; }
  Task<WaveStream?> Generate(XivMessage message);
  int ResolvePitch(XivMessage message);
  event System.Action? OnInitialized;
}

public partial class LocalTTSService(ILogger _logger, Configuration _configuration, IDataService _dataService, IGameInteropService _gameInteropService, IDalamudPluginInterface _pluginInterface) : ILocalTTSService
{
  public event System.Action? OnInitialized;
  public List<LocalTTSVoice> Voices { get; private set; } = [];

  private readonly KokoroTTSPipelineConfig _pipelineConfig = new(new DefaultSegmentationConfig()
  {
    MaxFirstSegmentLength = 510,
    MaxSecondSegmentLength = 510,
  });
  private bool _initialized;
  private KokoroModel? _model;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    if (_dataService.ToolsDirectory != null && IsToolsReady()) Initialize(_dataService.ToolsDirectory);
    _dataService.OnToolsDownloaded += OnToolsDownloaded;
    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _dataService.OnToolsDownloaded -= OnToolsDownloaded;
    return _logger.ServiceLifecycle();
  }

  private bool IsToolsReady()
  {
    if (_dataService.ToolsDirectory == null) return false;
    if (!File.Exists(Path.Join(_dataService.ToolsDirectory, "kokoro-quant.onnx"))) return false;
    if (!Directory.Exists(Path.Join(_dataService.ToolsDirectory, "voices"))) return false;
    if (!Directory.Exists(Path.Join(_dataService.ToolsDirectory, "espeak-ng-data"))) return false;
    return true;
  }

  private void OnToolsDownloaded()
  {
    if (_dataService.ToolsDirectory == null || !IsToolsReady()) return;
    Dispose();
    Initialize(_dataService.ToolsDirectory);
  }

  private void Initialize(string toolsDirectory)
  {
    if (_initialized) return;
    _initialized = true;
    _model ??= new KokoroModel(Path.Join(toolsDirectory, "kokoro-quant.onnx"));
    foreach (string filePath in Directory.GetFiles(Path.Join(toolsDirectory, "/voices")).Where(f => f.EndsWith(".npy")))
      Voices.Add(LocalTTSVoice.FromPath(filePath));
    InitializePhonemizer(toolsDirectory);
    InitializeTokenizer();
    OnInitialized?.Invoke();
  }

  private void Dispose()
  {
    if (!_initialized) return;
    _initialized = false;
    _model?.Dispose();
    _model = null;
    Voices.Clear();
    DisposePhonemizer();
  }

  public Task<WaveStream?> Generate(XivMessage message) => Task.Run(() => Generate_Internal(message));

  public int ResolvePitch(XivMessage message)
  {
    if (message.PitchOverride != null) return message.PitchOverride.Value;

    string speaker = message.Source == MessageSource.ChatMessage ? $"{message.Speaker}@{message.SpeakerWorld}" : message.Speaker;
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
    if (message.VoiceOverride != null)
    {
      LocalTTSVoice? match = Voices.FirstOrDefault(v => v.Name == message.VoiceOverride);
      if (match != null) return match;
    }

    string speaker = message.Source == MessageSource.ChatMessage ? $"{message.Speaker}@{message.SpeakerWorld}" : message.Speaker;
    if (_configuration.LocalTTSOverrides.TryGetValue(speaker, out (string voice, int pitch) options))
    {
      LocalTTSVoice? match = Voices.FirstOrDefault(v => v.Name == options.voice);
      if (match != null) return match;
    }

    string gender = message.Npc?.Gender ?? _configuration.LocalTTSDefaultVoice;
    if (_configuration.LocalTTSVoiceRandomization)
    {
      List<LocalTTSVoice> pool = Voices.Where(v => v.Gender == gender).ToList();
      byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(speaker));
      ulong v = BitConverter.ToUInt64(hash);
      return pool[(int)(v % (ulong)pool.Count)];
    }

    string defaultVoice = gender == "Male" ? _configuration.LocalTTSMaleVoice : _configuration.LocalTTSFemaleVoice;
    return Voices.FirstOrDefault(v => v.Name == defaultVoice);
  }

  private async Task<WaveStream?> Generate_Internal(XivMessage message)
  {
    if (!_initialized) return null;
    Stopwatch sw = Stopwatch.StartNew();

    LocalTTSVoice? voice = ResolveVoice(message);
    if (voice == null) return null;
    _logger.Debug($"Using LocalTTS Voice: {voice.Name}");

    int[] tokens = Tokenize(ApplyLexicon(message));
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

    MemoryStream ms = new();
    using (RawSourceWaveStream reader = new(new MemoryStream(pcm.ToArray()), new WaveFormat(24000, 16, 1)))
      WaveFileWriter.WriteWavFileToStream(ms, reader);
    ms.Position = 0;
    return new WaveFileReader(ms);
  }
}
