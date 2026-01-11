namespace XivVoices.Services;

public class TrackableSound : ISampleProvider, IDisposable
{
  private readonly ILogger _logger;

  private readonly ISampleProvider _innerProvider;
  private readonly VolumeSampleProvider _volumeProvider;
  private readonly PanningSampleProvider _panningProvider;

  private bool _playbackEnded = false;
  private bool _isPaused = false;
  
  private float _currentVolume = 1.0f;
  private float _currentPan = 0.0f;

  private TimeSpan _lastKnownTime = TimeSpan.Zero;
  private DateTime? _lastUpdateTime = null;

  public XivMessage Message { get; }
  public WaveStream SourceStream { get; }
  public bool IsStopping { get; set; } = false;

  public Action<TrackableSound>? OnPlaybackStopped;

  public TimeSpan CurrentTime => SourceStream.CurrentTime;
  public TimeSpan TotalTime => SourceStream.TotalTime;

  public TrackableSound(ILogger logger, XivMessage message, WaveStream sourceStream)
  {
    _logger = logger;
    Message = message;
    SourceStream = sourceStream;

    ISampleProvider sourceSampleProvider = sourceStream.ToSampleProvider();
    if (sourceSampleProvider.WaveFormat.SampleRate != 48000)
    {
      _logger.Debug($"Resampling from {sourceSampleProvider.WaveFormat.SampleRate}hz to 48000hz");
      sourceSampleProvider = new WdlResamplingSampleProvider(sourceSampleProvider, 48000);
    }
    _innerProvider = sourceSampleProvider;

    _volumeProvider = new VolumeSampleProvider(_innerProvider) { Volume = 1.0f };
    _panningProvider = new PanningSampleProvider(_volumeProvider) { Pan = 0.0f };

    _lastKnownTime = SourceStream.CurrentTime;
    _lastUpdateTime = DateTime.UtcNow;
  }

  public WaveFormat WaveFormat => _panningProvider.WaveFormat;

  public int Read(float[] buffer, int offset, int count)
  {
    if (_isPaused)
    {
      Array.Clear(buffer, offset, count);
      return count;
    }

    int read = _panningProvider.Read(buffer, offset, count);

    if (read > 0)
    {
      _lastKnownTime = SourceStream.CurrentTime;
      _lastUpdateTime = DateTime.UtcNow;
    }

    if (!_playbackEnded && (read == 0 || SourceStream.Position >= SourceStream.Length))
    {
      _playbackEnded = true;
      _lastKnownTime = TotalTime;
      _lastUpdateTime = null;
      OnPlaybackStopped?.Invoke(this);
    }

    return read;
  }

  public TimeSpan EstimatedCurrentTime
  {
    get
    {
      if (_lastUpdateTime is null || _playbackEnded)
        return TimeSpan.Zero;

      TimeSpan elapsed = DateTime.UtcNow - _lastUpdateTime.Value;
      TimeSpan estimated = _lastKnownTime + elapsed;

      return estimated > TotalTime ? TotalTime : estimated;
    }
  }

  public void Dispose() => SourceStream.Dispose();

  public bool IsPlaying => !IsStopping && !_playbackEnded && SourceStream.Position < SourceStream.Length;

  public bool IsPaused
  {
    get => _isPaused;
    set => _isPaused = value;
  }

  public float Volume
  {
    get => _currentVolume;
    set
    {
      _currentVolume = Math.Clamp(value, 0f, 1f);
      _volumeProvider.Volume = _currentVolume;
    }
  }

  public float Pan
  {
    get => _currentPan;
    set
    {
      _currentPan = Math.Clamp(value, -1f, 1f);
      _panningProvider.Pan = _currentPan;
    }
  }
}

