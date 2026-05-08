using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using Lumina.Extensions;

namespace XivVoices.Windows;

public class PlaybackSettingsTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.PlaybackSettings;
  private Configuration _configuration = null!;
  private IPlaybackService _playbackService = null!;
  private IMessageDispatcher _messageDispatcher = null!;

  private CheckboxNode _muteEnabledNode = null!;

  private LabelTextNode _outputDeviceNode = null!;
  private TextDropDownNode _playbackDeviceTypeNode = null!;
  private TextDropDownNode _waveOutDeviceNode = null!;
  private TextDropDownNode _directSoundDeviceNode = null!;

  private SliderNode _volumeSliderNode = null!;
  private SliderNode _speedSliderNode = null!;

  private SliderNode _ttsVolumeSliderNode = null!;
  private SliderNode _ttsSpeedSliderNode = null!;
  private TextDropDownNode _ttsDefaultVoiceNode = null!;

  private CheckboxNode _directionalAudioChatNode = null!;
  private CheckboxNode _directionalAudioBubblesNode = null!;
  private SliderNode _directionalAudioPanNode = null!;

  private RadioButtonGroupNode _unfocusedWindowBehaviorNode = null!;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();
    _playbackService = _services.GetRequiredService<IPlaybackService>();
    _messageDispatcher = _services.GetRequiredService<IMessageDispatcher>();

    _playbackService.OnOutputDeviceChanged += OnOutputDeviceChanged;

    ConfigSectionNode masterToggleSectionNode = new("Master Toggle");

    masterToggleSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      This is the preferred master toggle for this plugin.
      All features will be disabled except for automatic reports.
      You can also use "/xivv mute" or toggle it via the overlay.
      """,
    }, inline: true);

    _muteEnabledNode = new()
    {
      String = "Mute Enabled",
      Size = new Vector2(130.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.MuteEnabled = value;
        _configuration.Save();
      }
    };
    masterToggleSectionNode.AttachNode(_muteEnabledNode);

    AttachNode(masterToggleSectionNode);

    ConfigSectionNode outputDeviceSectionNode = new("Output Device", masterToggleSectionNode);

    outputDeviceSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Output Driver",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 2.0f);

    _playbackDeviceTypeNode = new TextDropDownNode()
    {
      Options = Enum.GetValues<PlaybackDeviceType>().Select((e) => e.ToString()).ToList(),
      Size = new Vector2(220.0f, 24.0f),
      X = 140.0f,
      OnOptionSelected = (option) =>
      {
        if (Enum.TryParse(option, out PlaybackDeviceType playbackDeviceType))
        {
          _configuration.PlaybackDeviceType = playbackDeviceType;
          _configuration.Save();
          _playbackService.InitializeOutputDevice();
        }
      }
    };
    outputDeviceSectionNode.AttachNode(_playbackDeviceTypeNode, inline: true, padding: -2.0f);

    _outputDeviceNode = new()
    {
      String = "Output Device",
      Height = 18.0f,
      FontSize = 14,
    };
    outputDeviceSectionNode.AttachNode(_outputDeviceNode, padding: 6.0f);

    _waveOutDeviceNode = new TextDropDownNode()
    {
      Options = [],
      Size = new Vector2(220.0f, 24.0f),
      X = 140.0f,
      OnOptionSelected = (option) =>
      {
        if (option == "Default Output Device")
        {
          _configuration.WaveOutDevice = null;
          _configuration.Save();
          _playbackService.InitializeOutputDevice();
          return;
        }

        WaveOutCapabilities? device = _playbackService.GetWaveOutDevices().FirstOrNull((d) => d.ProductName.Contains(option));
        if (device.HasValue)
        {
          _configuration.WaveOutDevice = device.Value.ProductName;
          _configuration.Save();
          _playbackService.InitializeOutputDevice();
        }
      }
    };
    outputDeviceSectionNode.AttachNode(_waveOutDeviceNode, inline: true, padding: -2.0f);

    _directSoundDeviceNode = new TextDropDownNode()
    {
      Options = [],
      Size = new Vector2(220.0f, 24.0f),
      X = 140.0f,
      OnOptionSelected = (option) =>
      {
        if (option == "Default Output Device")
        {
          _configuration.DirectSoundDevice = null;
          _configuration.Save();
          _playbackService.InitializeOutputDevice();
          return;
        }

        DirectSoundDeviceInfo? device = _playbackService.GetDirectSoundDevices().FirstOrDefault((d) => d.Description.Contains(option));
        if (device != null)
        {
          _configuration.DirectSoundDevice = device.Guid;
          _configuration.Save();
          _playbackService.InitializeOutputDevice();
        }
      }
    };
    outputDeviceSectionNode.AttachNode(_directSoundDeviceNode, inline: true, padding: -2.0f);

    outputDeviceSectionNode.AttachNode(new TextButtonNode
    {
      String = "Play Test Message",
      Size = new Vector2(220.0f, 28.0f),
      X = 140.0f,
      OnClick = () => _messageDispatcher.DispatchTestMessage(),
    }, indent: false, padding: 4.0f);

    AttachNode(outputDeviceSectionNode);

    ConfigSectionNode playbackSettingsSectionNode = new("Playback Settings", outputDeviceSectionNode);

    _volumeSliderNode = new SliderNode()
    {
      Range = 1..100,
      Size = new Vector2(220.0f, 16.0f),
      OnValueChanged = (value) =>
      {
        if (_configuration.Volume != value)
        {
          _configuration.Volume = value;
          _configuration.Save();
        }
      }
    };
    playbackSettingsSectionNode.AttachNode(_volumeSliderNode, padding: 5.0f);

    playbackSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Volume",
      X = 240.0f,
      Height = 16.0f,
      TextColor = ColorHelper.GetColor(2),
    }, inline: true);

    _speedSliderNode = new SliderNode()
    {
      Range = 50..200,
      Size = new Vector2(220.0f, 16.0f),
      OnValueChanged = (value) =>
      {
        if (_configuration.Speed != value)
        {
          _configuration.Speed = value;
          _configuration.Save();
        }
      }
    };
    playbackSettingsSectionNode.AttachNode(_speedSliderNode, padding: 5.0f);

    playbackSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Speed",
      X = 240.0f,
      Height = 16.0f,
      TextColor = ColorHelper.GetColor(2),
    }, inline: true);

    _ttsVolumeSliderNode = new SliderNode()
    {
      Range = 1..100,
      Size = new Vector2(220.0f, 16.0f),
      OnValueChanged = (value) =>
      {
        if (_configuration.LocalTTSVolume != value)
        {
          _configuration.LocalTTSVolume = value;
          _configuration.Save();
        }
      }
    };
    playbackSettingsSectionNode.AttachNode(_ttsVolumeSliderNode, padding: 5.0f);

    playbackSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "TTS Volume",
      X = 240.0f,
      Height = 16.0f,
      TextColor = ColorHelper.GetColor(2),
    }, inline: true);

    _ttsSpeedSliderNode = new SliderNode()
    {
      Range = 50..200,
      Size = new Vector2(220.0f, 16.0f),
      OnValueChanged = (value) =>
      {
        if (_configuration.LocalTTSSpeed != value)
        {
          _configuration.LocalTTSSpeed = value;
          _configuration.Save();
        }
      }
    };
    playbackSettingsSectionNode.AttachNode(_ttsSpeedSliderNode, padding: 5.0f);

    playbackSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "TTS Speed",
      X = 240.0f,
      Height = 16.0f,
      TextColor = ColorHelper.GetColor(2),
    }, inline: true);

    playbackSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Default Voice",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 5.0f);

    _ttsDefaultVoiceNode = new TextDropDownNode()
    {
      Options = ["Male", "Female"],
      X = 140.0f,
      Size = new Vector2(220.0f, 24.0f),
      OnOptionSelected = (option) =>
      {
        _configuration.LocalTTSDefaultVoice = option;
        _configuration.Save();
      }
    };
    playbackSettingsSectionNode.AttachNode(_ttsDefaultVoiceNode, inline: true);

    AttachNode(playbackSettingsSectionNode);

    ConfigSectionNode directionalAudioSettingsSectionNode = new("Directional Audio Settings", playbackSettingsSectionNode);

    _directionalAudioChatNode = new()
    {
      String = "Directional Audio (Chat)",
      Size = new Vector2(200.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.DirectionalAudioForChat = value;
        _configuration.Save();
      }
    };
    directionalAudioSettingsSectionNode.AttachNode(_directionalAudioChatNode);

    _directionalAudioBubblesNode = new()
    {
      String = "Directional Audio (Bubbles)",
      Size = new Vector2(230.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.DirectionalAudioForAddonMiniTalk = value;
        _configuration.Save();
      }
    };
    directionalAudioSettingsSectionNode.AttachNode(_directionalAudioBubblesNode);

    _directionalAudioPanNode = new SliderNode()
    {
      Range = 1..100,
      Size = new Vector2(220.0f, 16.0f),
      OnValueChanged = (value) =>
      {
        if (_configuration.MaximumPan != value)
        {
          _configuration.MaximumPan = value;
          _configuration.Save();
        }
      }
    };
    directionalAudioSettingsSectionNode.AttachNode(_directionalAudioPanNode, padding: 5.0f);

    directionalAudioSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Maximum Pan",
      X = 240.0f,
      Height = 16.0f,
      TextColor = ColorHelper.GetColor(2),
    }, inline: true);

    AttachNode(directionalAudioSettingsSectionNode);

    ConfigSectionNode unfocusedWindowBehaviorSectionNode = new("Unfocused Window Behavior", directionalAudioSettingsSectionNode);

    _unfocusedWindowBehaviorNode = new()
    {
      Width = 100f,
    };

    _unfocusedWindowBehaviorNode.AddButton(GetUnfocusedWindowBehaviourString(UnfocusedBehavior.Play), () =>
    {
      _configuration.UnfocusedBehavior = UnfocusedBehavior.Play;
      _configuration.Save();
    });

    _unfocusedWindowBehaviorNode.AddButton(GetUnfocusedWindowBehaviourString(UnfocusedBehavior.Pause), () =>
    {
      _configuration.UnfocusedBehavior = UnfocusedBehavior.Pause;
      _configuration.Save();
    });

    _unfocusedWindowBehaviorNode.AddButton(GetUnfocusedWindowBehaviourString(UnfocusedBehavior.Mute), () =>
    {
      _configuration.UnfocusedBehavior = UnfocusedBehavior.Mute;
      _configuration.Save();
    });

    unfocusedWindowBehaviorSectionNode.AttachNode(_unfocusedWindowBehaviorNode);

    unfocusedWindowBehaviorSectionNode.AttachNode(new ConfigTooltipNode()
    {
      Y = 18,
      TextTooltip = "Voicelines will play like normal when the window is unfocused.",
    }, inline: true);

    unfocusedWindowBehaviorSectionNode.AttachNode(new ConfigTooltipNode()
    {
      Y = 38,
      TextTooltip = "Voicelines will be paused entirely until window is refocused.",
    }, inline: true);

    unfocusedWindowBehaviorSectionNode.AttachNode(new ConfigTooltipNode()
    {
      Y = 58,
      TextTooltip = "Voicelines will still continue playback, but they will be muted.",
    }, inline: true);

    AttachNode(unfocusedWindowBehaviorSectionNode);
  }

  protected override void Dispose(bool disposing, bool isNativeDestructor)
  {
    base.Dispose(disposing, isNativeDestructor);

    _playbackService.OnOutputDeviceChanged -= OnOutputDeviceChanged;
  }

  private void OnOutputDeviceChanged(object? sender, bool initialized)
  {
    _outputDeviceNode.TextColor = ColorHelper.GetColor(initialized ? 8u : 15u);
  }

  public override void ConfigurationSaved()
  {
    _muteEnabledNode.IsChecked = _configuration.MuteEnabled;

    _playbackDeviceTypeNode.SelectedOption = _configuration.PlaybackDeviceType.ToString();

    _waveOutDeviceNode.IsVisible = _configuration.PlaybackDeviceType == PlaybackDeviceType.WaveOut;
    _waveOutDeviceNode.Options = ["Default Output Device", .. _playbackService.GetWaveOutDevices().Select((d) => ShortenDeviceName(d.ProductName))];
    _waveOutDeviceNode.SelectedOption = ShortenDeviceName(_configuration.WaveOutDevice ?? "Default Output Device");

    IEnumerable<DirectSoundDeviceInfo> directSoundDevices = _playbackService.GetDirectSoundDevices();
    _directSoundDeviceNode.IsVisible = _configuration.PlaybackDeviceType == PlaybackDeviceType.DirectSound;
    _directSoundDeviceNode.Options = ["Default Output Device", .. directSoundDevices.Select((d) => ShortenDeviceName(d.Description))];
    _directSoundDeviceNode.SelectedOption = ShortenDeviceName(directSoundDevices.FirstOrDefault((d) => d.Guid == _configuration.DirectSoundDevice)?.Description ?? "Default Output Device");

    _volumeSliderNode.Value = _configuration.Volume;
    NativeUtils.FixSliderNode(_volumeSliderNode);

    _speedSliderNode.Value = _configuration.Speed;
    NativeUtils.FixSliderNode(_speedSliderNode);

    _ttsVolumeSliderNode.Value = _configuration.LocalTTSVolume;
    NativeUtils.FixSliderNode(_ttsVolumeSliderNode);

    _ttsSpeedSliderNode.Value = _configuration.LocalTTSSpeed;
    NativeUtils.FixSliderNode(_ttsSpeedSliderNode);

    _ttsDefaultVoiceNode.SelectedOption = _configuration.LocalTTSDefaultVoice;

    _directionalAudioChatNode.IsChecked = _configuration.DirectionalAudioForChat;
    _directionalAudioBubblesNode.IsChecked = _configuration.DirectionalAudioForAddonMiniTalk;

    _directionalAudioPanNode.Value = _configuration.MaximumPan;
    NativeUtils.FixSliderNode(_directionalAudioPanNode);

    _unfocusedWindowBehaviorNode.SelectedOption = GetUnfocusedWindowBehaviourString(_configuration.UnfocusedBehavior);

    OnOutputDeviceChanged(null, _playbackService.IsOutputDeviceInitialized);
  }

  private string ShortenDeviceName(string device) =>
    device.Replace("Speakers (", "").Replace(")", "");

  private string GetUnfocusedWindowBehaviourString(UnfocusedBehavior value) =>
    value switch
    {
      UnfocusedBehavior.Play => "Play voicelines while unfocused",
      UnfocusedBehavior.Pause => "Pause voicelines while unfocused",
      UnfocusedBehavior.Mute => "Mute voicelines while unfocused",
      _ => "",
    };
}
