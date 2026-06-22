using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public class DebugTabPanelNode(IServiceProvider _services) : TabPanelNode
{
  public override ConfigTab Tab => ConfigTab.Debug;
  private Configuration _configuration = null!;
  private IDataService _dataService = null!;
  private IPlaybackService _playbackService = null!;

  private LabelTextNode _currentPlayingNode = null!;
  private LabelTextNode _mixerSourcesNode = null!;
  private CheckboxNode _debugLoggingNode = null!;
  private ConfigTextEditNode _serverUrlNode = null!;
  private CheckboxNode _liveModeNode = null!;
  private CheckboxNode _warnIgnoredSpeakerNode = null!;
  private TextDropDownNode _defaultChatChannelNoe = null!;

  private ConfigOverlayNode _overlayNode = null!;
  private TextInputNode _overlayInputNode = null!;
  private System.Action? _onOverlaySubmit = null;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();
    _dataService = _services.GetRequiredService<IDataService>();
    _playbackService = _services.GetRequiredService<IPlaybackService>();

    ConfigSectionNode debugSectionNode = new("Debug");

    _currentPlayingNode = new()
    {
      Size = new Vector2(200.0f, 20.0f)
    };
    debugSectionNode.AttachNode(_currentPlayingNode);

    _mixerSourcesNode = new()
    {
      Size = new Vector2(200.0f, 20.0f)
    };
    debugSectionNode.AttachNode(_mixerSourcesNode);

    _debugLoggingNode = new()
    {
      String = "Debug Logging",
      Size = new Vector2(140.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.DebugLogging = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_debugLoggingNode);

    _serverUrlNode = new("ServerUrl")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.ServerUrl;
        _overlayNode.Title = "Edit ServerUrl";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _dataService.SetServerUrl(_overlayInputNode.String.ToString());
        };
      }
    };
    debugSectionNode.AttachNode(_serverUrlNode);

    _liveModeNode = new()
    {
      String = "LiveMode",
      Size = new Vector2(100.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LiveMode = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_liveModeNode);

    _warnIgnoredSpeakerNode = new()
    {
      String = "WarnIgnoredSpeaker",
      Size = new Vector2(180.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.WarnIgnoredSpeaker = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_warnIgnoredSpeakerNode);

    debugSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Default Channel",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 2.0f);

    _defaultChatChannelNoe = new()
    {
      Size = new Vector2(220.0f, 24.0f),
      X = 140.0f,
      Options = Enum.GetValues<XivChatType>().Select(e => e.ToString()).ToList(),
      OnOptionSelected = (option) =>
      {
        if (Enum.TryParse(option, out XivChatType chatChannel))
        {
          _configuration.DefaultChatChannel = chatChannel;
          _configuration.Save();
        }
      }
    };
    debugSectionNode.AttachNode(_defaultChatChannelNoe, inline: true);

    AttachNode(debugSectionNode);

    _overlayNode = new ConfigOverlayNode(_services);
    _overlayNode.AttachNode(this);

    _overlayInputNode = new TextInputNode()
    {
      Size = new Vector2(300.0f, 30.0f),
    };
    _overlayNode.AttachContent(_overlayInputNode);

    _overlayNode.AttachContent(new TextButtonNode
    {
      String = "Submit",
      Size = new Vector2(60.0f, 28.0f),
      Position = new Vector2(0.0f, 32.0f),
      OnClick = () =>
      {
        _onOverlaySubmit?.Invoke();
        _overlayNode.IsVisible = false;
      }
    });

    _overlayNode.AttachContent(new TextButtonNode
    {
      String = "Cancel",
      Size = new Vector2(60.0f, 28.0f),
      Position = new Vector2(60.0f, 32.0f),
      OnClick = () => _overlayNode.IsVisible = false,
    });
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _overlayNode.SetSize(Size);
  }

  public override void OnUpdate()
  {
    _currentPlayingNode.String = $"Currently Playing: {_playbackService.Debug_GetPlaying().Count()}";
    _mixerSourcesNode.String = $"Mixer Sources: {_playbackService.Debug_GetMixerSourceCount()}";
  }

  public override void ConfigurationSaved()
  {
    _debugLoggingNode.IsChecked = _configuration.DebugLogging;
    _serverUrlNode.Value = _configuration.ServerUrl ?? "";
    _liveModeNode.IsChecked = _configuration.LiveMode;
    _warnIgnoredSpeakerNode.IsChecked = _configuration.WarnIgnoredSpeaker;
    _defaultChatChannelNoe.SelectedOption = _configuration.DefaultChatChannel.ToString();
  }
}
