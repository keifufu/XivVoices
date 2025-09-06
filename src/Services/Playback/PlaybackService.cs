using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace XivVoices.Services;

public interface IPlaybackService : IHostedService
{
  event EventHandler<XivMessage>? PlaybackStarted;
  event EventHandler<XivMessage>? PlaybackCompleted;

  Task Play(XivMessage message, bool replay = false);

  void StopAll();
  void Stop(MessageSource source);
  void Stop(string id);
  void Skip();

  bool IsPlaying(MessageSource source);

  IEnumerable<(XivMessage message, bool isPlaying, float percentage)> GetPlaybackHistory();

  IEnumerable<TrackableSound> Debug_GetPlaying();
  int Debug_GetMixerSourceCount();
}

public class PlaybackService(ILogger _logger, Configuration _configuration, ILipSync _lipSync, ILocalTTSService _localTTSService, IAudioPostProcessor _audioPostProcessor, IGameInteropService _gameInteropService, IFramework _framework, IClientState _clientState) : IPlaybackService
{
  private WaveOutEvent? _outputDevice;
  private MixingSampleProvider? _mixer;

  private readonly ConcurrentDictionary<string, TrackableSound> _playing = new();
  private readonly object _playbackHistoryLock = new();
  private readonly List<XivMessage> _playbackHistory = [];

  public event EventHandler<XivMessage>? PlaybackStarted;
  public event EventHandler<XivMessage>? PlaybackCompleted;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _framework.Update += FrameworkOnUpdate;
    _clientState.TerritoryChanged += OnTerritoryChanged;

    _outputDevice = new WaveOutEvent();
    _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2))
    {
      ReadFully = true
    };

    _outputDevice.Init(_mixer);
    _outputDevice.Play();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _framework.Update -= FrameworkOnUpdate;
    _clientState.TerritoryChanged -= OnTerritoryChanged;

    foreach (TrackableSound track in _playing.Values)
    {
      _mixer?.RemoveMixerInput(track);
      track.Dispose();
    }

    _playing.Clear();
    _outputDevice?.Stop();
    _outputDevice?.Dispose();
    _outputDevice = null;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void FrameworkOnUpdate(IFramework framework)
  {
    foreach (TrackableSound track in _playing.Values)
      UpdateTrack(track);
  }

  private void OnTerritoryChanged(ushort _)
  {
    StopAll();
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

        // Can't let bubbles get too quiet in duties.
        if (_gameInteropService.IsInDuty())
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
    if (_mixer == null || _outputDevice == null)
    {
      _logger.Error("Mixer or OutputDevice were not initialited.");
      return;
    }

    string? voicelinePath = message.VoicelinePath;
    if (message.IsLocalTTS) voicelinePath = await _localTTSService.WriteLocalTTSToDisk(message);
    if (voicelinePath == null) return; // LocalTTS generation failed

    // Since TTS can take some time to generate, this solves some headaches for now.
    if (message.Source == MessageSource.AddonTalk && !_configuration.QueueDialogue)
      Stop(MessageSource.AddonTalk);

    WaveStream? sourceStream = await _audioPostProcessor.PostProcessToPCM(voicelinePath, message.IsLocalTTS, message);
    if (message.IsLocalTTS) File.Delete(voicelinePath);
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
        if (_playbackHistory.Count > 100)
          _playbackHistory.RemoveAt(_playbackHistory.Count - 1);
      }
    }
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

  public bool IsPlaying(MessageSource source)
  {
    foreach (TrackableSound track in _playing.Values)
    {
      if (track.Message.Source == source)
        return true;
    }

    return false;
  }

  public IEnumerable<(XivMessage message, bool isPlaying, float percentage)> GetPlaybackHistory()
  {
    lock (_playbackHistoryLock)
    {
      foreach (XivMessage message in _playbackHistory)
      {
        if (_playing.TryGetValue(message.Id, out TrackableSound? track))
          yield return (message, track.IsPlaying, (float)(track.EstimatedCurrentTime.TotalMilliseconds / track.TotalTime.TotalMilliseconds));
        else
          yield return (message, false, 100);
      }
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
