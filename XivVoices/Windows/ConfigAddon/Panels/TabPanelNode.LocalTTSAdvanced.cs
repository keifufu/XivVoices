using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public class LocalTTSAdvancedTabPanelNode(IServiceProvider _services) : TabPanelNode
{
  public override ConfigTab Tab => ConfigTab.LocalTTSAdvanced;
  private Configuration _configuration = null!;

  private StatelessTabBarNode _tabBarNode = null!;

  private CheckboxNode _localTTSForcedNode = null!;
  private CheckboxNode _localTTSRemoteEnabledNode = null!;
  private CheckboxNode _localTTSRemoteFPSLimitNode = null!;
  private ConfigTextEditNode _localTTSRemoteUriNode = null!;

  private ConfigOverlayNode _overlayNode = null!;
  private TextInputNode _overlayInputNode = null!;
  private System.Action? _onOverlaySubmit = null;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();

    _tabBarNode = new();
    _tabBarNode.AddTab("Settings", () => SetTab?.Invoke(ConfigTab.LocalTTSSettings));
    _tabBarNode.AddTab("Lexicon", () => SetTab?.Invoke(ConfigTab.LocalTTSLexicon));
    _tabBarNode.AddTab("Advanced", () => SetTab?.Invoke(ConfigTab.LocalTTSAdvanced), isSelected: true);
    AttachNode(_tabBarNode);

    ConfigSectionNode advancedSettingsSectionNode = new("Advanced Settings", offset: 26.0f);

    _localTTSForcedNode = new CheckboxNode
    {
      String = "Force LocalTTS",
      TextTooltip = "Everything will be voiced by LocalTTS",
      Size = new Vector2(130.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSForced = value;
        _configuration.Save();
      }
    };
    advancedSettingsSectionNode.AttachNode(_localTTSForcedNode);

    AttachNode(advancedSettingsSectionNode);
    ConfigSectionNode remoteSectionNode = new("Remote Generation", advancedSettingsSectionNode);

    remoteSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      Lets you hook up any external TTS provider.
      Modify the URI below according to your requirements.
      Arguments:
      - %n : XIVV NPC Name (or null)
      - %v : XIVV Voice Name (or null)
      - %s : Speaker
      - %t : Sentence
      The response is expected to be a WAV file.
      """,
    }, inline: true);

    _localTTSRemoteEnabledNode = new CheckboxNode
    {
      String = "Enable Remote Generation",
      Size = new Vector2(220.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSRemoteEnabled = value;
        _configuration.Save();
      }
    };
    remoteSectionNode.AttachNode(_localTTSRemoteEnabledNode);

    _localTTSRemoteFPSLimitNode = new CheckboxNode
    {
      String = "Limit FPS during Generation",
      Size = new Vector2(230.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSRemoteFPSLimit = value;
        _configuration.Save();
      }
    };
    remoteSectionNode.AttachNode(_localTTSRemoteFPSLimitNode);

    _localTTSRemoteUriNode = new("Remote LocalTTS URI")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.LocalTTSRemoteUri;
        _overlayNode.Title = "Edit Remote Generation URI";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.LocalTTSRemoteUri = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    remoteSectionNode.AttachNode(_localTTSRemoteUriNode);

    AttachNode(remoteSectionNode);

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

    _tabBarNode.Size = new Vector2(Width, 24.0f);
    _overlayNode.SetSize(Size);
  }

  public override void ConfigurationSaved()
  {
    _localTTSForcedNode.IsChecked = _configuration.LocalTTSForced;
    _localTTSRemoteEnabledNode.IsChecked = _configuration.LocalTTSRemoteEnabled;
    _localTTSRemoteFPSLimitNode.IsChecked = _configuration.LocalTTSRemoteFPSLimit;
    _localTTSRemoteFPSLimitNode.IsEnabled = _configuration.LocalTTSRemoteEnabled;
    _localTTSRemoteUriNode.Value = _configuration.LocalTTSRemoteUri;
    _localTTSRemoteUriNode.IsEnabled = _configuration.LocalTTSRemoteEnabled;
  }
}
