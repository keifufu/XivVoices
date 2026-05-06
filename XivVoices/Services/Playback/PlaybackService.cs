using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Extensions;
using FrameworkStruct = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace XivVoices.Services;

public interface IPlaybackService : IHostedService
{
  event EventHandler<XivMessage>? PlaybackStarted;
  event EventHandler<XivMessage>? PlaybackCompleted;
  event EventHandler<XivMessage>? QueuedLineSkipped;

  bool Paused { get; set; }

  public void InitializeOutputDevice();
  public IEnumerable<WaveOutCapabilities> GetWaveOutDevices();
  public IEnumerable<DirectSoundDeviceInfo> GetDirectSoundDevices();

  Task Play(XivMessage message, bool replay = false);

  void StopAll();
  void Stop(MessageSource source);
  void Stop(string id);
  void Skip();

  int CountPlaying(MessageSource source);

  IEnumerable<(XivMessage message, bool isPlaying, float percentage, bool isQueued)> GetPlaybackHistory();
  XivMessage? GetLatestCurrentlyPlayingMessage();

  void AddQueuedLine(XivMessage message);
  void SkipQueuedLine(XivMessage message);
  void RemoveQueuedLine(XivMessage message);
  void ClearQueue();

  IEnumerable<TrackableSound> Debug_GetPlaying();
  int Debug_GetMixerSourceCount();
}

public class PlaybackService(ILogger _logger, Configuration _configuration, ILipSync _lipSync, IDataService _dataService, ILocalTTSService _localTTSService, IAudioPostProcessor _audioPostProcessor, IGameInteropService _gameInteropService, IFramework _framework, IObjectTable _objectTable) : IPlaybackService
{
  private WaveOutEvent? _waveOutputDevice;
  private DirectSoundOut? _directSoundOutputDevice;
  private MixingSampleProvider? _mixer;

  private readonly ConcurrentDictionary<string, TrackableSound> _playing = new();
  private readonly object _playbackHistoryLock = new();
  private readonly List<XivMessage> _playbackHistory = [];
  private readonly List<XivMessage> _queuedMessages = [];

  public event EventHandler<XivMessage>? PlaybackStarted;
  public event EventHandler<XivMessage>? PlaybackCompleted;
  public event EventHandler<XivMessage>? QueuedLineSkipped;

  private bool _paused = false;
  public bool Paused
  {
    get => _paused || (_configuration.UnfocusedBehavior == UnfocusedBehavior.Pause && IsWindowUnfocused);
    set => _paused = value;
  }

  private bool _wasWindowUnfocused = false;
  private unsafe bool IsWindowUnfocused => FrameworkStruct.Instance() != null && FrameworkStruct.Instance()->WindowInactive;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _framework.Update += FrameworkOnUpdate;

    InitializeOutputDevice();

    return _logger.ServiceLifecycle();
  }

  private void StopOutputDevice()
  {
    _waveOutputDevice?.Stop();
    _waveOutputDevice?.Dispose();
    _waveOutputDevice = null;
    _directSoundOutputDevice?.Stop();
    _directSoundOutputDevice?.Dispose();
    _directSoundOutputDevice = null;
  }

  public void InitializeOutputDevice()
  {
    StopAll();
    StopOutputDevice();

    _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2))
    {
      ReadFully = true
    };

    try
    {
      switch (_configuration.PlaybackDeviceType)
      {
        case PlaybackDeviceType.WaveOut:
          InitializeWaveOutDevice();
          break;
        case PlaybackDeviceType.DirectSound:
          InitializeDirectSoundDevice();
          break;
      }
    }
    catch (Exception ex)
    {
      StopOutputDevice();
      _logger.Error(ex);
      // Could try to initialize the opposite of what the user configured if it fails,
      // but I'd rather just the user fix their configuration, so we will just warn here.
      _logger.Chat(pre: "Output device failed to initialize.", preColor: 15, post: "Please check your output configuration.");
    }
  }

  private void InitializeWaveOutDevice()
  {
    int devices = WaveOut.DeviceCount;
    _logger.Debug($"[Wave] Available audio devices: {devices}");

    int deviceNumber = -1;
    for (int i = -1; i < devices; i++)
    {
      WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(i);
      _logger.Debug($"[Wave] Device {i} ({deviceInfo.ProductName})");

      if (deviceInfo.ProductName == _configuration.WaveOutDevice)
        deviceNumber = i;
    }

    _waveOutputDevice = new WaveOutEvent()
    {
      DeviceNumber = deviceNumber,
    };
    _logger.Debug($"[Wave] Initializing WaveOutEvent with Device {_configuration.WaveOutDevice}");
    _waveOutputDevice.Init(_mixer);
    _waveOutputDevice.Play();
  }

  private void InitializeDirectSoundDevice()
  {
    _directSoundOutputDevice = new DirectSoundOut(_configuration.DirectSoundDevice ?? DirectSoundOut.DSDEVID_DefaultPlayback);

    foreach (DirectSoundDeviceInfo? device in DirectSoundOut.Devices)
    {
      _logger.Debug($"[DirectSound] Device {device.Guid} ({device.Description})");
    }

    _logger.Debug($"[DirectSound] Initializing DirectSoundOut with Device {_configuration.DirectSoundDevice ?? DirectSoundOut.DSDEVID_DefaultPlayback}");
    _directSoundOutputDevice.Init(_mixer);
    _directSoundOutputDevice.Play();
  }

  public IEnumerable<WaveOutCapabilities> GetWaveOutDevices()
  {
    for (int i = -1; i < WaveOut.DeviceCount; i++)
      yield return WaveOut.GetCapabilities(i);
  }

  public IEnumerable<DirectSoundDeviceInfo> GetDirectSoundDevices()
  {
    foreach (DirectSoundDeviceInfo? device in DirectSoundOut.Devices)
      if (device != null) yield return device;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _framework.Update -= FrameworkOnUpdate;

    foreach (TrackableSound track in _playing.Values)
    {
      _mixer?.RemoveMixerInput(track);
      track.Dispose();
    }

    foreach (XivMessage message in _queuedMessages)
    {
      message.GenerationToken.Cancel();
    }

    _playing.Clear();
    StopOutputDevice();

    return _logger.ServiceLifecycle();
  }

  private void FrameworkOnUpdate(IFramework framework)
  {
    if (_wasWindowUnfocused != IsWindowUnfocused)
    {
      if (_waveOutputDevice != null && _waveOutputDevice.PlaybackState != PlaybackState.Stopped)
        if (Paused) _waveOutputDevice.Pause(); else _waveOutputDevice.Play();
      if (_directSoundOutputDevice != null && _directSoundOutputDevice.PlaybackState != PlaybackState.Stopped)
        if (Paused) _directSoundOutputDevice.Pause(); else _directSoundOutputDevice.Play();

      _wasWindowUnfocused = IsWindowUnfocused;
    }

    foreach (TrackableSound track in _playing.Values)
      UpdateTrack(track);
  }

  private unsafe Task UpdateTrack(TrackableSound track)
  {
    return _gameInteropService.RunOnFrameworkThread(() =>
    {
      if (track.IsStopping) return;
      track.IsMuted = _configuration.UnfocusedBehavior == UnfocusedBehavior.Mute && IsWindowUnfocused;
      track.Volume = (track.Message.IsLocalTTS ? _configuration.LocalTTSVolume : _configuration.Volume) / 100f;

      if (
        (track.Message.Source == MessageSource.AddonMiniTalk && _configuration.DirectionalAudioForAddonMiniTalk) ||
        (track.Message.Source == MessageSource.ChatMessage && _configuration.DirectionalAudioForChat)
      )
      {
        if (_objectTable.LocalPlayer == null) return;
        if (track.Message.Speaker == _objectTable.LocalPlayer.Name.ToString()) return;
        Vector3 playerPosition = _objectTable.LocalPlayer.Position;

        Character* speaker = (Character*)_gameInteropService.TryFindCharacter(track.Message.Speaker, track.Message.Npc?.BaseId ?? 0);
        if (speaker == null) return;
        Vector3 speakerPosition = new(speaker->Position.X, speaker->Position.Y, speaker->Position.Z);

        Vector3 relativePosition = speakerPosition - playerPosition;
        float distance = relativePosition.Length();

        CameraView camera = _gameInteropService.GetCameraView();

        float dotProduct = Vector3.Dot(relativePosition, camera.Right);
        float balance = Math.Clamp(dotProduct / (distance > 0 ? distance : 1), _configuration.MaximumPan / 100 * -1, _configuration.MaximumPan / 100);

        float volume = track.Volume;

        (float distanceStart, float distanceEnd, float volumeStart, float volumeEnd)[] volumeRanges =
        [
          (0f, 3f, volume * 1f, volume * 0.85f), // 0 to 3 units: 100% to 85%
          (3f, 5f, volume * 0.85f, volume * 0.3f), // 3 to 5 units: 85% to 30%
          (5f, 20f, volume * 0.3f, volume * 0.05f) // 5 to 20 units: 30% to 5%
        ];

        // Can't let bubbles get too quiet in duties, or for directional chat messages.
        if (_gameInteropService.IsInDuty() || track.Message.Source == MessageSource.ChatMessage)
        {
          volumeRanges[0].volumeStart = 0.65f * volume;
          volumeRanges[0].volumeEnd = 0.63f * volume; // 0 to 3 units: 65% to 63%
          volumeRanges[1].volumeStart = 0.63f * volume;
          volumeRanges[1].volumeEnd = 0.60f * volume; // 3 to 5 units: 63% to 60%
          volumeRanges[2].volumeStart = 0.60f * volume;
          volumeRanges[2].volumeEnd = 0.55f * volume; // 5 to 20 units: 60% to 55%
        }

        foreach ((float distanceStart, float distanceEnd, float volumeStart, float volumeEnd) in volumeRanges)
        {
          if (distance >= distanceStart && distance <= distanceEnd)
          {
            float slope = (volumeEnd - volumeStart) / (distanceEnd - distanceStart);
            float yIntercept = volumeStart - (slope * distanceStart);
            float _volume = (slope * distance) + yIntercept;
            volume = Math.Clamp(_volume, Math.Min(volumeStart, volumeEnd), Math.Max(volumeStart, volumeEnd));
            break;
          }
        }

        if (volume == track.Volume)
          volume = volumeRanges[^1].volumeEnd;

        // _logger.Debug($"Updating track: volume::{volume} pan::{balance}");

        track.Volume = volume;
        track.Pan = balance;
      }
    });
  }

  public async Task Play(XivMessage message, bool replay = false)
  {
    if (_mixer == null || (_waveOutputDevice == null && _directSoundOutputDevice == null))
    {
      _logger.Error("Mixer or OutputDevice were not initialited.");
      return;
    }

    // It seems output devices stop after some inactivity. Couln't replicate this on linux buuuut whatever?
    if (_waveOutputDevice?.PlaybackState == PlaybackState.Stopped || _directSoundOutputDevice?.PlaybackState == PlaybackState.Stopped)
    {
      _logger.Debug("Output device was stopped, initializing it again.");
      InitializeOutputDevice();
    }

    string? voicelinePath = message.VoicelinePath;

    message.IsGenerating = true;
    message.GenerationToken = new();
    lock (_playbackHistoryLock)
    {
      if (_playbackHistory.FirstOrDefault((m) => m.Id == message.Id) == default)
        _queuedMessages.Add(message);
    }

    bool isIgnoredSpeaker = _dataService.Manifest?.IgnoredSpeakers.Contains(message.Speaker) ?? false;
    if (!message.IsFake && !isIgnoredSpeaker && voicelinePath == null && _configuration.LiveMode && message.Source == MessageSource.AddonTalk)
    {
      voicelinePath = await TryDownloadVoiceline(message);
      message.VoicelinePath = voicelinePath;
    }

    // New token incase livemode failed or was cancelled, so we fall back to localtts/localgen.
    message.GenerationToken = new();
    bool useLocalTTS = message.IsLocalTTS && !_configuration.EnableLocalGeneration && !_configuration.ForceLocalGeneration;
    bool useLocalGen = (_configuration.EnableLocalGeneration && _configuration.ForceLocalGeneration) || message.IsLocalTTS && _configuration.EnableLocalGeneration;

    if (useLocalTTS) voicelinePath = await _localTTSService.WriteLocalTTSToDisk(message);
    else if (useLocalGen) voicelinePath = await localGen(message);

    // Generation failed
    if (voicelinePath == null)
    {
      string method = useLocalTTS ? "LocalTTS" : "LocalGen";
      _logger.Error($"Generation failed. Method: {method}. Message: {message.Id}");
      lock (_playbackHistoryLock)
      {
        _queuedMessages.Remove(message);
      }
      message.IsGenerating = false;
      return;
    }

    if (useLocalGen) message.VoicelinePath = voicelinePath;

    // Since TTS can take some time to generate, this solves some headaches for now.
    if (message.Source == MessageSource.AddonTalk && !_configuration.QueueDialogue)
      Stop(MessageSource.AddonTalk);

    WaveStream? sourceStream = await _audioPostProcessor.PostProcessToPCM(voicelinePath, message.IsLocalTTS, message);
    if (useLocalTTS) File.Delete(voicelinePath);
    lock (_playbackHistoryLock)
    {
      _queuedMessages.Remove(message);
    }
    message.IsGenerating = false;

    if (sourceStream == null)
    {
      _logger.Debug($"AudioPostProcessor failed. Message: {message.Id}");
      return;
    }

    if (_playing.TryRemove(message.Id, out TrackableSound? oldTrack))
    {
      _mixer.RemoveMixerInput(oldTrack);
      oldTrack.Dispose();
    }

    TrackableSound track = new(_logger, message, sourceStream);
    await UpdateTrack(track);
    track.OnPlaybackStopped += t =>
    {
      // Apparently no need to call .RemoveMixerInput, it seems to automagically remove itself
      // when playback is completed. Calling .RemoveMixerInput here does not cause any exceptions
      // but any future lines will be broken.
      t.Dispose();

      // Sleep a little before finishing the playback and thus auto advancing.
      // I personally don't think this is needed but after trimming audio files of silence
      // some people are of the opinion there needs to be a bit of a pause after the line.
      Thread.Sleep(200);

      _playing.TryRemove(message.Id, out _);
      _logger.Debug($"Finished playing message: {message.Id}");

      PlaybackCompleted?.Invoke(this, message);
    };

    _logger.Debug($"Starting playing message: {message.Id}");
    _logger.Debug($"Output volume: {_waveOutputDevice?.Volume}, {_directSoundOutputDevice?.Volume}");
    _logger.Debug($"Output state: {_waveOutputDevice?.PlaybackState}, {_directSoundOutputDevice?.PlaybackState}");

    PlaybackStarted?.Invoke(this, message);
    _mixer.AddMixerInput(track);
    _playing[message.Id] = track;

    if (_configuration.LipSyncEnabled)
      _ = _lipSync.TryLipSync(message, track.TotalTime.TotalSeconds);

    if (!replay)
    {
      lock (_playbackHistoryLock)
      {
        int existingIndex = _playbackHistory.FindIndex(m => m.Id == message.Id);
        if (existingIndex != -1)
          _playbackHistory.RemoveAt(existingIndex);

        _playbackHistory.Insert(0, message);
        // if (_playbackHistory.Count > 100)
        //   _playbackHistory.RemoveAt(_playbackHistory.Count - 1);
      }
    }
  }

  private async Task<string?> localGen(XivMessage message)
  {
    if (message.GenerationToken.IsCancellationRequested) return null;

    if (message.Voice != null)
    {
      if (_dataService.Manifest == null) return null;

      string requestUri = _configuration.LocalGenerationUri
        .Replace("%v", Uri.EscapeDataString(message.Voice.Id))
        .Replace("%s", Uri.EscapeDataString(message.AddName(message.Sentence)))
        .Replace("%i", Uri.EscapeDataString(message.Id));

      if (_configuration.LimitFpsDuringLocalGeneration) unsafe { FrameworkStruct.Instance()->WindowInactive = true; }
      HttpResponseMessage response = await _dataService.HttpClient.GetAsync(requestUri, message.GenerationToken.Token);
      if (_configuration.LimitFpsDuringLocalGeneration) unsafe { FrameworkStruct.Instance()->WindowInactive = false; }

      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadAsStringAsync();
      }
    }

    return null;
  }

  private async Task<string?> TryDownloadVoiceline(XivMessage message)
  {
    if (message.GenerationToken == null) return null;

    // using CancellationTokenSource ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(message.GenerationToken.Token);
    // ctsTimeout.CancelAfter(TimeSpan.FromMinutes(5));
    // CancellationToken token = ctsTimeout.Token;
    CancellationToken token = message.GenerationToken.Token;

    while (!token.IsCancellationRequested)
    {
      string requestUri = $"{_dataService.ServerUrl}/files/lookup/{Uri.EscapeDataString(message.Speaker)}/{Uri.EscapeDataString(message.Sentence)}";

      try
      {
        using HttpResponseMessage response = await _dataService.HttpClient.GetAsync(requestUri, message.GenerationToken.Token);

        if (response.IsSuccessStatusCode)
        {
          string? fileName = response.Content.Headers.ContentDisposition?.FileName;
          if (fileName == null) return null;

          if (_dataService.VoicelinesDirectory == null) return null;
          string voicelinePath = Path.Join(_dataService.VoicelinesDirectory, fileName);

          using (FileStream fileStream = new(voicelinePath, FileMode.Create, FileAccess.Write, FileShare.None))
            await response.Content.CopyToAsync(fileStream);

          return voicelinePath;
        }
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested)
      {
        return null;
      }
      catch (HttpRequestException)
      {
        try { await Task.Delay(TimeSpan.FromSeconds(0.5), token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return null; }
      }
    }

    return null;
  }

  public void StopAll()
  {
    _logger.Debug($"Stopping all playing audio");
    foreach (TrackableSound track in _playing.Values)
      _ = FadeOutAndStopAsync(track);
  }

  public void Stop(MessageSource source)
  {
    _logger.Debug($"Stopping playing audio from source {source}");
    foreach (TrackableSound track in _playing.Values)
      if (track.Message.Source == source)
        _ = FadeOutAndStopAsync(track);
  }

  public void Stop(string id)
  {
    if (_playing.TryRemove(id, out TrackableSound? track))
      _ = FadeOutAndStopAsync(track);
    else
      _logger.Debug($"Failed to find playing audio with id {id}");
  }

  public void Skip()
  {
    _logger.Debug("Skipping the most recently playing voiceline.");

    KeyValuePair<string, TrackableSound> lastTrack = _playing.LastOrDefault();
    if (lastTrack.Value != null)
      _ = FadeOutAndStopAsync(lastTrack.Value);
  }

  public int CountPlaying(MessageSource source)
  {
    int count = 0;
    foreach (TrackableSound track in _playing.Values)
      if (track.Message.Source == source)
        count++;

    return count;
  }

  public IEnumerable<(XivMessage message, bool isPlaying, float percentage, bool isQueued)> GetPlaybackHistory()
  {
    lock (_playbackHistoryLock)
    {
      List<XivMessage> queuedMessages = _queuedMessages.ToList();
      foreach (XivMessage message in queuedMessages)
      {
        yield return (message, false, 0, true);
      }

      List<XivMessage> playbackHistory = _playbackHistory.ToList();
      foreach (XivMessage message in playbackHistory)
      {
        if (_playing.TryGetValue(message.Id, out TrackableSound? track))
          yield return (message, track.IsPlaying, (float)(track.EstimatedCurrentTime.TotalMilliseconds / track.TotalTime.TotalMilliseconds), false);
        else
          yield return (message, false, message.IsGenerating ? 0 : 1, false);
      }
    }
  }

  public XivMessage? GetLatestCurrentlyPlayingMessage()
  {
    return _playing.FirstOrNull()?.Value.Message;
  }

  public void AddQueuedLine(XivMessage message)
  {
    lock (_playbackHistoryLock)
    {
      _queuedMessages.Insert(0, message);
    }
  }

  public void SkipQueuedLine(XivMessage message)
  {
    lock (_playbackHistoryLock)
    {
      message.GenerationToken.Cancel();
      _queuedMessages.Remove(message);
      QueuedLineSkipped?.Invoke(this, message);
    }
  }

  public void RemoveQueuedLine(XivMessage message)
  {
    lock (_playbackHistoryLock)
    {
      _queuedMessages.Remove(message);
    }
  }

  public void ClearQueue()
  {
    lock (_playbackHistoryLock)
    {
      _queuedMessages.Clear();
    }
  }

  public IEnumerable<TrackableSound> Debug_GetPlaying()
  {
    foreach (TrackableSound track in _playing.Values)
      yield return track;
  }

  public int Debug_GetMixerSourceCount()
  {
    return _mixer?.MixerInputs.Count() ?? -1;
  }

  private async Task FadeOutAndStopAsync(TrackableSound track, int fadeDurationMs = 150)
  {
    if (track.IsStopping) return;
    track.IsStopping = true;

    track.Message.GenerationToken.Cancel();
    _lipSync.TryStopLipSync(track.Message);

    const int intervalMs = 25;
    int steps = fadeDurationMs / intervalMs;
    float initialVolume = track.Volume;

    for (int i = 0; i < steps; i++)
    {
      float newVolume = initialVolume * (1 - ((float)(i + 1) / steps));
      track.Volume = newVolume;
      await Task.Delay(intervalMs);
    }

    track.Volume = 0;

    _mixer?.RemoveMixerInput(track);
    track.OnPlaybackStopped = null;
    track.Dispose();

    string key = _playing.FirstOrDefault(kvp => kvp.Value == track).Key;
    if (key != null)
      _playing.TryRemove(key, out _);

    PlaybackCompleted?.Invoke(this, track.Message);

    _logger.Debug("Track faded out and stopped.");
  }
}
