using SoundTouch.Net.NAudioSupport;

namespace XivVoices.Services;

public class TrackableSound : ISampleProvider, IDisposable
{
  private readonly ILogger _logger;

  private readonly ISampleProvider _innerProvider;
  private readonly VolumeSampleProvider _volumeProvider;
  private readonly PanningSampleProvider _panningProvider;
  private readonly SoundTouchWaveProvider _soundTouchProvider;
  private readonly WaveStream _sourceStream;

  private bool _playbackEnded = false;

  private readonly Lock _positionLock = new();
  private long _lastSamplesPlayedAtRead;
  private DateTime _lastReadTimeUtc;
  private long _samplesPlayed;

  public XivMessage Message { get; }
  public bool IsStopping { get; set; } = false;
  public bool IsMuted = false;

  public Action<TrackableSound>? OnPlaybackStopped;

  public TrackableSound(ILogger logger, XivMessage message, WaveStream sourceStream)
  {
    Message = message;
    _logger = logger;
    _sourceStream = sourceStream;

    ISampleProvider sourceSampleProvider = _sourceStream.ToSampleProvider();
    if (sourceSampleProvider.WaveFormat.SampleRate != 48000)
    {
      _logger.Debug($"Resampling from {sourceSampleProvider.WaveFormat.SampleRate}hz to 48000hz");
      sourceSampleProvider = new WdlResamplingSampleProvider(sourceSampleProvider, 48000);
    }

    _innerProvider = sourceSampleProvider;
    _volumeProvider = new VolumeSampleProvider(_innerProvider) { Volume = 1.0f };
    _panningProvider = new PanningSampleProvider(_volumeProvider) { Pan = 0.0f };
    _soundTouchProvider = new SoundTouchWaveProvider(_panningProvider.ToWaveProvider());

    _samplesPlayed = 0;
    _lastSamplesPlayedAtRead = 0;
    _lastReadTimeUtc = DateTime.UtcNow;
  }

  public WaveFormat WaveFormat => _soundTouchProvider.WaveFormat;
  public TimeSpan TotalTime => _sourceStream.TotalTime;

  public int Read(float[] buffer, int offset, int count)
  {
    int read = _soundTouchProvider.ToSampleProvider().Read(buffer, offset, count);

    if (read > 0)
    {
      lock (_positionLock)
      {
        _samplesPlayed += read;
        _lastSamplesPlayedAtRead = _samplesPlayed;
        _lastReadTimeUtc = DateTime.UtcNow;
      }
    }

    if (!_playbackEnded && read < count)
    {
      _playbackEnded = true;
      Task.Run(() => OnPlaybackStopped?.Invoke(this));
    }

    return read;
  }

  public TimeSpan EstimatedCurrentTime
  {
    get
    {
      if (_playbackEnded) return TotalTime;
      double elapsedSec = (DateTime.UtcNow - _lastReadTimeUtc).TotalSeconds;
      if (elapsedSec <= 0) elapsedSec = 0;
      long extrapolatedSamples = _lastSamplesPlayedAtRead + (long)(elapsedSec * WaveFormat.SampleRate * WaveFormat.Channels);
      double seconds = (double)extrapolatedSamples / (WaveFormat.SampleRate * WaveFormat.Channels);
      TimeSpan estimated = TimeSpan.FromSeconds(seconds);
      TimeSpan result = TimeSpan.FromSeconds(Math.Min(estimated.TotalSeconds, TotalTime.TotalSeconds) * (Speed / 100.0));
      return result > TotalTime ? TotalTime : result;
    }
  }

  public void Dispose() => _sourceStream.Dispose();

  public bool IsPlaying => !IsStopping && !_playbackEnded && _sourceStream.Position < _sourceStream.Length;

  public float Volume
  {
    get => IsMuted ? 0 : field;
    set
    {
      field = Math.Clamp(value, 0f, 3f);
      _volumeProvider.Volume = IsMuted ? 0f : Math.Clamp(field * (1f + (RelativeVolume / 100f)), 0f, 3f);
    }
  } = 0; // Start with 0 volume until it gets set correctly.

  public float RelativeVolume
  {
    get;
    set
    {
      field = value;
      _volumeProvider.Volume = IsMuted ? 0f : Math.Clamp(Volume * (1f + (RelativeVolume / 100f)), 0f, 3f);
    }
  } = 0;

  public float Pan
  {
    get;
    set
    {
      field = Math.Clamp(value, -1f, 1f);
      _panningProvider.Pan = value;
    }
  } = 0.0f;

  public double Speed
  {
    get => _soundTouchProvider.TempoChange + 100;
    set
    {
      _soundTouchProvider.TempoChange = value - 100;
    }
  }

  public double Pitch
  {
    get => _soundTouchProvider.Pitch * 100;
    set
    {
      _soundTouchProvider.Pitch = value / 100;
    }
  }
}
