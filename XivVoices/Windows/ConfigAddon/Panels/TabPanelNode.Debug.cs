using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class DebugTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.Debug;
  private Configuration _configuration = null!;
  private IDataService _dataService = null!;
  private IPlaybackService _playbackService = null!;

  private LabelTextNode _currentPlayingNode = null!;
  private LabelTextNode _mixerSourcesNode = null!;
  private CheckboxNode _debugLoggingNode = null!;
  private ConfigTextEditNode _serverUrlNode = null!;
  private ConfigTextEditNode _localTTSVoiceMaleNode = null!;
  private ConfigTextEditNode _localTTSVoiceFemaleNode = null!;
  private CheckboxNode _useStreamElementsLocalTTSNode = null!;
  private ConfigTextEditNode _streamElementsApiKeyNode = null!;
  private ConfigTextEditNode _streamElementsMaleVoiceNode = null!;
  private ConfigTextEditNode _streamElementsFemaleVoiceNode = null!;
  private CheckboxNode _enableLocalGenerationNode = null!;
  private CheckboxNode _forceLocalGenerationNode = null!;
  private CheckboxNode _limitFpsDuringLocalGenerationNode = null!;
  private ConfigTextEditNode _localGenerationUriNode = null!;
  private CheckboxNode _superFastForwardNode = null!;
  private CheckboxNode _liveModeNode = null!;
  private CheckboxNode _warnIgnoredSpeakerNode = null!;

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

    _localTTSVoiceMaleNode = new("LocalTTSVoiceMale")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.LocalTTSVoiceMale;
        _overlayNode.Title = "Edit LocalTTSVoiceMale";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.LocalTTSVoiceMale = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    debugSectionNode.AttachNode(_localTTSVoiceMaleNode);

    _localTTSVoiceFemaleNode = new("LocalTTSVoiceFemale")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.LocalTTSVoiceFemale;
        _overlayNode.Title = "Edit LocalTTSVoiceFemale";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.LocalTTSVoiceFemale = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    debugSectionNode.AttachNode(_localTTSVoiceFemaleNode);

    _useStreamElementsLocalTTSNode = new()
    {
      String = "UseStreamElementsLocalTTS",
      Size = new Vector2(240.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.UseStreamElementsLocalTTS = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_useStreamElementsLocalTTSNode);

    _streamElementsApiKeyNode = new("StreamElementsApiKey")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.StreamElementsApiKey;
        _overlayNode.Title = "Edit StreamElementsApiKey";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.StreamElementsApiKey = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    debugSectionNode.AttachNode(_streamElementsApiKeyNode);

    _streamElementsMaleVoiceNode = new("StreamElementsMaleVoice")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.StreamElementsMaleVoice;
        _overlayNode.Title = "Edit StreamElementsMaleVoice";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.StreamElementsMaleVoice = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    debugSectionNode.AttachNode(_streamElementsMaleVoiceNode);

    _streamElementsFemaleVoiceNode = new("StreamElementsFemaleVoice")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.StreamElementsFemaleVoice;
        _overlayNode.Title = "Edit StreamElementsFemaleVoice";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.StreamElementsFemaleVoice = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    debugSectionNode.AttachNode(_streamElementsFemaleVoiceNode);

    _enableLocalGenerationNode = new()
    {
      String = "EnableLocalGeneration",
      Size = new Vector2(200.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.EnableLocalGeneration = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_enableLocalGenerationNode);

    _forceLocalGenerationNode = new()
    {
      String = "ForceLocalGeneration",
      Size = new Vector2(190.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.ForceLocalGeneration = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_forceLocalGenerationNode);

    _limitFpsDuringLocalGenerationNode = new()
    {
      String = "LimitFpsDuringLocalGeneration",
      Size = new Vector2(260.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LimitFpsDuringLocalGeneration = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_limitFpsDuringLocalGenerationNode);

    _localGenerationUriNode = new("LocalGenerationUri")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.LocalGenerationUri;
        _overlayNode.Title = "Edit LocalGenerationUri";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.LocalGenerationUri = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    debugSectionNode.AttachNode(_localGenerationUriNode);

    _superFastForwardNode = new()
    {
      String = "SuperFastForward",
      Size = new Vector2(160.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.SuperFastForward = value;
        _configuration.Save();
      }
    };
    debugSectionNode.AttachNode(_superFastForwardNode);

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
    _localTTSVoiceMaleNode.Value = _configuration.LocalTTSVoiceMale;
    _localTTSVoiceFemaleNode.Value = _configuration.LocalTTSVoiceFemale;
    _useStreamElementsLocalTTSNode.IsChecked = _configuration.UseStreamElementsLocalTTS;
    _streamElementsApiKeyNode.Value = _configuration.StreamElementsApiKey;
    _streamElementsMaleVoiceNode.Value = _configuration.StreamElementsMaleVoice;
    _streamElementsFemaleVoiceNode.Value = _configuration.StreamElementsFemaleVoice;
    _enableLocalGenerationNode.IsChecked = _configuration.EnableLocalGeneration;
    _forceLocalGenerationNode.IsChecked = _configuration.ForceLocalGeneration;
    _limitFpsDuringLocalGenerationNode.IsChecked = _configuration.LimitFpsDuringLocalGeneration;
    _localGenerationUriNode.Value = _configuration.LocalGenerationUri;
    _superFastForwardNode.IsChecked = _configuration.SuperFastForward;
    _liveModeNode.IsChecked = _configuration.LiveMode;
    _warnIgnoredSpeakerNode.IsChecked = _configuration.WarnIgnoredSpeaker;
  }
}
