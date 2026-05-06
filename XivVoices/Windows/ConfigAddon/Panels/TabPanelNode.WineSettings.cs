using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class WineSettingsTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.WineSettings;
  private Configuration _configuration = null!;
  private IAudioPostProcessor _audioPostProcessor = null!;

  private CheckboxNode _useNativeFFmpegNode = null!;
  private ConfigTextEditNode _protonUsernameNode = null!;

  private LabelTextNode _ffmpegStatusNode = null!;

  private ConfigSectionNode _ffmpegTroubleshootingSectionNode = null!;

  private ConfigOverlayNode _overlayNode = null!;
  private TextInputNode _overlayInputNode = null!;
  private System.Action? _onOverlaySubmit = null;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();
    _audioPostProcessor = _services.GetRequiredService<IAudioPostProcessor>();

    ConfigSectionNode ffmpegSettingsSectionNode = new("FFmpeg Settings");

    _useNativeFFmpegNode = new()
    {
      String = "Use native FFmpeg",
      Size = new Vector2(170.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.WineUseNativeFFmpeg = value;
        _configuration.Save();
        if (value) _audioPostProcessor.FFmpegStart();
        else _audioPostProcessor.FFmpegStop();
      }
    };
    ffmpegSettingsSectionNode.AttachNode(_useNativeFFmpegNode);

    ffmpegSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = "Increases processing speed and prevents lag spikes on voices with effects (e.g. Dragons) and when using a playback speed other than 100.",
    }, inline: true);

    _protonUsernameNode = new("Proton Username")
    {
      Size = new Vector2(110.0f, 24.0f),
      OnClick = () =>
      {
        _overlayInputNode.String = _configuration.ProtonUsername;
        _overlayNode.Title = "Edit Proton Username";
        _overlayNode.IsVisible = true;
        _onOverlaySubmit = () =>
        {
          _configuration.ProtonUsername = _overlayInputNode.String.ToString();
          _configuration.Save();
        };
      }
    };
    ffmpegSettingsSectionNode.AttachNode(_protonUsernameNode);

    ffmpegSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = "If you're using proton, you might have to enter your linux username here. (case-sensitive)",
    }, inline: true);

    AttachNode(ffmpegSettingsSectionNode);

    ConfigSectionNode ffmpegStatusSectionNode = new("FFmpeg Status", ffmpegSettingsSectionNode);

    _ffmpegStatusNode = new()
    {
      Size = new Vector2(200.0f, 20.0f)
    };
    ffmpegStatusSectionNode.AttachNode(_ffmpegStatusNode);

    ffmpegStatusSectionNode.AttachNode(new TextButtonNode
    {
      String = "Start",
      Size = new Vector2(60.0f, 28.0f),
      OnClick = () => _audioPostProcessor.FFmpegStart(),
    });

    ffmpegStatusSectionNode.AttachNode(new TextButtonNode
    {
      String = "Stop",
      Size = new Vector2(60.0f, 28.0f),
      X = 80.0f,
      OnClick = () => _audioPostProcessor.FFmpegStop(),
    }, inline: true);

    ffmpegStatusSectionNode.AttachNode(new TextButtonNode
    {
      String = "Refresh",
      Size = new Vector2(80.0f, 28.0f),
      X = 140.0f,
      OnClick = () => _audioPostProcessor.RefreshFFmpegWineProcessState(),
    }, inline: true);

    ffmpegStatusSectionNode.AttachNode(new TextButtonNode
    {
      String = "Copy Start Command",
      Size = new Vector2(150.0f, 28.0f),
      X = 220.0f,
      OnClick = () => ImGui.SetClipboardText($"bash \"{_audioPostProcessor.FFmpegWineScriptPath}\" {_audioPostProcessor.FFmpegWineProcessPort} & disown"),
    }, inline: true);

    AttachNode(ffmpegStatusSectionNode);

    _ffmpegTroubleshootingSectionNode = new("Troubleshooting", ffmpegStatusSectionNode);

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Does \"/usr/bin/env\" exist?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"bash\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"ss\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"ffmpeg\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"pgrep\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"grep\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"wc\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is \"ncat\" installed?",
      Size = new Vector2(200.0f, 20.0f)
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "not \"netcat\" nor \"nc\".",
      Size = new Vector2(200.0f, 20.0f),
      X = ConfigSectionNode.Indent,
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "\"ncat\" is usually part of the \"nmap\" package.",
      Size = new Vector2(200.0f, 20.0f),
      X = ConfigSectionNode.Indent,
    });

    _ffmpegTroubleshootingSectionNode.AttachNode(new LabelTextNode
    {
      String = "- Is port 1469 already in use?",
      Size = new Vector2(200.0f, 20.0f)
    });

    AttachNode(_ffmpegTroubleshootingSectionNode);

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
    _ffmpegStatusNode.String = "FFmpeg daemon state: " + (_audioPostProcessor.FFmpegWineProcessRunning ? "Running" : "Stopped");
    _ffmpegTroubleshootingSectionNode.IsVisible = !_audioPostProcessor.FFmpegWineProcessRunning;
  }

  public override void ConfigurationSaved()
  {
    _useNativeFFmpegNode.IsChecked = _configuration.WineUseNativeFFmpeg;
    _protonUsernameNode.Value = _configuration.ProtonUsername;
  }
}
