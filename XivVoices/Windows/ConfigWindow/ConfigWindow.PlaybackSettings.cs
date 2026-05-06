namespace XivVoices.Windows;

public partial class ConfigWindow
{
  // FFmpeg 'atempo' limitations.
  private readonly int _minSpeed = 50;
  private readonly int _maxSpeed = 200;

  private void DrawPlaybackSettingsTab()
  {
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Master Toggle");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    bool muteEnabled = _configuration.MuteEnabled;
    DrawConfigCheckbox("Mute Enabled", ref muteEnabled);
    if (muteEnabled != _configuration.MuteEnabled)
    {
      _configuration.MuteEnabled = muteEnabled;
      _configuration.Save();
      if (_configuration.MuteEnabled)
      {
        _messageDispatcher.ClearQueue();
        _playbackService.ClearQueue();
        _playbackService.StopAll();
      }
    }

    ImGui.Dummy(ScaledVector2(0, 10));
    PlaybackDeviceType[] playbackDeviceTypes = Enum.GetValues<PlaybackDeviceType>();
    using (ImRaii.ComboDisposable combo = ImRaii.Combo("##PlaybackDeviceType", _configuration.PlaybackDeviceType.ToString()))
    {
      if (combo.Success)
      {
        for (int i = 0; i < playbackDeviceTypes.Length; i++)
        {
          if (ImGui.Selectable(playbackDeviceTypes[i].ToString(), _configuration.PlaybackDeviceType == playbackDeviceTypes[i]))
          {
            _configuration.PlaybackDeviceType = playbackDeviceTypes[i];
            _configuration.Save();
            _playbackService.InitializeOutputDevice();
          }
        }
      }
    }

    if (_configuration.PlaybackDeviceType == PlaybackDeviceType.WaveOut)
    {
      IEnumerable<WaveOutCapabilities> devices = _playbackService.GetWaveOutDevices();
      using (ImRaii.ComboDisposable combo = ImRaii.Combo("##WaveOutDevices", _configuration.WaveOutDevice ?? "Default Output Device"))
      {
        if (combo.Success)
        {
          if (ImGui.Selectable("Default Output Device", _configuration.WaveOutDevice == null))
          {
            _configuration.WaveOutDevice = null;
            _configuration.Save();
            _playbackService.InitializeOutputDevice();
          }

          for (int i = 0; i < devices.Count(); i++)
          {
            if (ImGui.Selectable($"{devices.ElementAt(i).ProductName}##{i}", _configuration.WaveOutDevice == devices.ElementAt(i).ProductName))
            {
              _configuration.WaveOutDevice = devices.ElementAt(i).ProductName;
              _configuration.Save();
              _playbackService.InitializeOutputDevice();
            }
          }
        }
      }
    }

    if (_configuration.PlaybackDeviceType == PlaybackDeviceType.DirectSound)
    {
      IEnumerable<DirectSoundDeviceInfo> devices = _playbackService.GetDirectSoundDevices();
      DirectSoundDeviceInfo? selectedDevice = devices.FirstOrDefault((d) => d.Guid == _configuration.DirectSoundDevice);
      using (ImRaii.ComboDisposable combo = ImRaii.Combo("##DirectSoundDevices", selectedDevice?.Description ?? "Default Output Device"))
      {
        if (combo.Success)
        {
          if (ImGui.Selectable("Default Output Device", _configuration.DirectSoundDevice == null))
          {
            _configuration.DirectSoundDevice = null;
            _configuration.Save();
            _playbackService.InitializeOutputDevice();
          }

          for (int i = 0; i < devices.Count(); i++)
          {
            if (ImGui.Selectable($"{devices.ElementAt(i).Description}##{i}", _configuration.DirectSoundDevice == devices.ElementAt(i).Guid))
            {
              _configuration.DirectSoundDevice = devices.ElementAt(i).Guid;
              _configuration.Save();
              _playbackService.InitializeOutputDevice();
            }
          }
        }
      }
    }

    if (ImGui.Button("Play Test Message"))
    {
      _messageDispatcher.DispatchTestMessage();
    }

    ImGui.Dummy(ScaledVector2(0, 15));
    ImGui.TextWrapped("Playback Settings");
    ImGui.Dummy(ScaledVector2(0, 5));

    ImGui.Dummy(ScaledVector2(0, 10));
    DrawConfigSlider("Volume", ref _configuration.Volume, 0, 100);
    DrawConfigSlider("Speed", ref _configuration.Speed, _minSpeed, _maxSpeed);

    DrawConfigSlider("TTS Volume", ref _configuration.LocalTTSVolume, 0, 100);
    DrawConfigSlider("TTS Speed", ref _configuration.LocalTTSSpeed, _minSpeed, _maxSpeed);

    ImGui.Dummy(ScaledVector2(0, 10));
    string[] genders = ["Male", "Female"];
    ImGui.Text("TTS Default Voice");
    using (ImRaii.ComboDisposable combo = ImRaii.Combo("##LocalTTSDefaultVoice", _configuration.LocalTTSDefaultVoice))
    {
      if (combo.Success)
      {
        for (int i = 0; i < genders.Length; i++)
        {
          if (ImGui.Selectable(genders[i]))
          {
            _configuration.LocalTTSDefaultVoice = genders[i];
            _configuration.Save();
          }
        }
      }
    }

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 25));
    ImGui.TextWrapped("Directional Audio");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Directional Audio (Chat)", ref _configuration.DirectionalAudioForChat);
    DrawConfigCheckbox("Directional Audio (Bubbles)", ref _configuration.DirectionalAudioForAddonMiniTalk);
    DrawConfigSlider("Pan %", ref _configuration.MaximumPan, 0, 100);

    ImGui.Dummy(ScaledVector2(0, 15));
    ImGui.TextWrapped("Unfocused Window Behavior");
    ImGui.Dummy(ScaledVector2(0, 5));

    if (ImGui.RadioButton("Play voicelines while unfocused", _configuration.UnfocusedBehavior == UnfocusedBehavior.Play))
    {
      _configuration.UnfocusedBehavior = UnfocusedBehavior.Play;
      _configuration.Save();
    }

    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Voicelines will play like normal when the window is unfocused.");

    if (ImGui.RadioButton("Pause voicelines while unfocused", _configuration.UnfocusedBehavior == UnfocusedBehavior.Pause))
    {
      _configuration.UnfocusedBehavior = UnfocusedBehavior.Pause;
      _configuration.Save();
    }

    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Voicelines will be paused entirely until window is refocused.");

    if (ImGui.RadioButton("Mute voicelines while unfocused", _configuration.UnfocusedBehavior == UnfocusedBehavior.Mute))
    {
      _configuration.UnfocusedBehavior = UnfocusedBehavior.Mute;
      _configuration.Save();
    }

    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Voicelines will still continue playback, but they will be muted.");
  }
}
