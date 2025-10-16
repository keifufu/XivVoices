using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace XivVoices.Services;

public interface IPlaybackService : IHostedService
{
  event EventHandler<XivMessage>? PlaybackStarted;
  event EventHandler<XivMessage>? PlaybackCompleted;
  event EventHandler<XivMessage>? QueuedLineSkipped;

  Task Play(XivMessage message, bool replay = false);

  void StopAll();
  void Stop(MessageSource source);
  void Stop(string id);
  void Skip();

  int CountPlaying(MessageSource source);

  IEnumerable<(XivMessage message, bool isPlaying, float percentage, bool isQueued)> GetPlaybackHistory();

  void AddQueuedLine(XivMessage message);
  void SkipQueuedLine(XivMessage message);
  void RemoveQueuedLine(XivMessage message);
  void ClearQueue();

  IEnumerable<TrackableSound> Debug_GetPlaying();
  int Debug_GetMixerSourceCount();
}

public class PlaybackService(ILogger _logger, Configuration _configuration, ILipSync _lipSync, IDataService _dataService, ILocalTTSService _localTTSService, IAudioPostProcessor _audioPostProcessor, IGameInteropService _gameInteropService, IFramework _framework, IClientState _clientState) : IPlaybackService
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

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _framework.Update += FrameworkOnUpdate;

    _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2))
    {
      ReadFully = true
    };

    try
    {
      int devices = WaveOut.DeviceCount;
      _logger.Debug($"[Wave] Available audio devices: {devices}");

      for (int i = -1; i < devices; i++)
      {
        WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(i);
        _logger.Debug($"[Wave] Device {i} ({deviceInfo.ProductName})");
      }

      _waveOutputDevice = new WaveOutEvent();
      _logger.Debug($"[Wave] Initializing WaveOutEvent with Device {_waveOutputDevice.DeviceNumber}");
      _waveOutputDevice.Init(_mixer);
      _waveOutputDevice.Play();
    }
    catch (Exception ex)
    {
      _logger.Error(ex);
      _logger.Debug("Failed to initialize WaveOutEvent, attempting DirectSoundOut.");

      _directSoundOutputDevice = new DirectSoundOut();

      foreach (DirectSoundDeviceInfo? device in DirectSoundOut.Devices)
      {
        _logger.Debug($"[DirectSound] Device {device.Guid} ({device.Description})");
      }

      _logger.Debug($"[DirectSound] Initializing DirectSoundOut with Device {DirectSoundOut.DSDEVID_DefaultPlayback} (DSDEVID_DefaultPlayback)");
      _directSoundOutputDevice.Init(_mixer);
      _directSoundOutputDevice.Play();
    }

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _framework.Update -= FrameworkOnUpdate;

    foreach (TrackableSound track in _playing.Values)
    {
      _mixer?.RemoveMixerInput(track);
      track.Dispose();
    }

    _playing.Clear();
    _waveOutputDevice?.Stop();
    _waveOutputDevice?.Dispose();
    _waveOutputDevice = null;
    _directSoundOutputDevice?.Stop();
    _directSoundOutputDevice?.Dispose();
    _directSoundOutputDevice = null;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void FrameworkOnUpdate(IFramework framework)
  {
    foreach (TrackableSound track in _playing.Values)
      UpdateTrack(track);
  }

  private unsafe Task UpdateTrack(TrackableSound track)
  {
    return _gameInteropService.RunOnFrameworkThread(() =>
    {
      if (track.IsStopping) return;
      track.Volume = (track.Message.IsLocalTTS ? _configuration.LocalTTSVolume : _configuration.Volume) / 100f;

      if (
        (track.Message.Source == MessageSource.AddonMiniTalk && _configuration.DirectionalAudioForAddonMiniTalk) ||
        (track.Message.Source == MessageSource.ChatMessage && _configuration.DirectionalAudioForChat)
      )
      {
        if (_clientState.LocalPlayer == null) return;
        if (track.Message.Speaker == _clientState.LocalPlayer.Name.ToString()) return;
        Vector3 playerPosition = _clientState.LocalPlayer.Position;

        Character* speaker = (Character*)_gameInteropService.TryFindCharacter(track.Message.Speaker, track.Message.Npc?.BaseId ?? 0);
        if (speaker == null) return;
        Vector3 speakerPosition = new(speaker->Position.X, speaker->Position.Y, speaker->Position.Z);

        Vector3 relativePosition = speakerPosition - playerPosition;
        float distance = relativePosition.Length();

        CameraView camera = _gameInteropService.GetCameraView();

        float dotProduct = Vector3.Dot(relativePosition, camera.Right);
        float balance = Math.Clamp(dotProduct / 20, -0.95f, 0.95f);

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
          volumeRanges[0].volumeStart = 0.65f;
          volumeRanges[0].volumeEnd = 0.63f; // 0 to 3 units: 65% to 63%
          volumeRanges[1].volumeStart = 0.63f;
          volumeRanges[1].volumeEnd = 0.60f; // 3 to 5 units: 63% to 60%
          volumeRanges[2].volumeStart = 0.60f;
          volumeRanges[2].volumeEnd = 0.55f; // 5 to 20 units: 60% to 55%
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

    string? voicelinePath = message.VoicelinePath;

    message.IsGenerating = true;
    message.GenerationToken = new();
    lock (_playbackHistoryLock)
    {
      if (_playbackHistory.FirstOrDefault((m) => m.Id == message.Id) == default)
        _queuedMessages.Add(message);
    }

    bool useLocalTTS = message.IsLocalTTS && !_configuration.EnableLocalGeneration && !_configuration.ForceLocalGeneration;
    bool useLocalGen = (_configuration.EnableLocalGeneration && _configuration.ForceLocalGeneration) || message.IsLocalTTS && _configuration.EnableLocalGeneration;

    if (useLocalTTS) voicelinePath = await _localTTSService.WriteLocalTTSToDisk(message);
    else if (useLocalGen) await localGen(message);
    if (voicelinePath == null) // generation failed
    {
      _queuedMessages.Remove(message);
      message.IsGenerating = false;
      return;
    }

    // Since TTS can take some time to generate, this solves some headaches for now.
    if (message.Source == MessageSource.AddonTalk && !_configuration.QueueDialogue)
      Stop(MessageSource.AddonTalk);

    WaveStream? sourceStream = await _audioPostProcessor.PostProcessToPCM(voicelinePath, message.IsLocalTTS, message);
    if (useLocalTTS) File.Delete(voicelinePath);

    _queuedMessages.Remove(message);
    message.IsGenerating = false;

    if (sourceStream == null) return; // AudioPostProcessor failed

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
      _playing.TryRemove(message.Id, out _);
      _logger.Debug($"Finished playing message: {message.Id}");

      PlaybackCompleted?.Invoke(this, message);
    };

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
        .Replace("%s", Uri.EscapeDataString(message.Sentence))
        .Replace("%i", Uri.EscapeDataString(message.Id));

      HttpResponseMessage response = await _dataService.HttpClient.GetAsync(requestUri, message.GenerationToken.Token);

      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadAsStringAsync();
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
      foreach (XivMessage message in _queuedMessages)
      {
        yield return (message, false, 0, true);
      }

      foreach (XivMessage message in _playbackHistory)
      {
        if (_playing.TryGetValue(message.Id, out TrackableSound? track))
          yield return (message, track.IsPlaying, (float)(track.EstimatedCurrentTime.TotalMilliseconds / track.TotalTime.TotalMilliseconds), false);
        else
          yield return (message, false, message.IsGenerating ? 0 : 100, false);
      }
    }
  }

  public void AddQueuedLine(XivMessage message)
  {
    _queuedMessages.Insert(0, message);
  }

  public void SkipQueuedLine(XivMessage message)
  {
    message.GenerationToken.Cancel();
    _queuedMessages.Remove(message);
    QueuedLineSkipped?.Invoke(this, message);
  }

  public void RemoveQueuedLine(XivMessage message)
  {
    _queuedMessages.Remove(message);
  }

  public void ClearQueue()
  {
    _queuedMessages.Clear();
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
