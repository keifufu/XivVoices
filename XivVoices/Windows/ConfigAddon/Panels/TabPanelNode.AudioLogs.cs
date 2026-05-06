using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class AudioLogsTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.AudioLogs;
  private Configuration _configuration = null!;
  private IReportService _reportService = null!;
  private IPlaybackService _playbackService = null!;

  private CheckboxNode _enableAutomaticReportsNode = null!;
  private CheckboxNode _logReportsToChatNode = null!;

  private ConfigSectionNode _audioLogsSectionNode = null!;
  private AudioListNode<(XivMessage message, bool isPlaying, float percentage, bool isQueued), AudioLogNode> _audioLogsNode = null!;
  private LabelTextNode _noAudioLogsNode = null!;

  private ConfigOverlayNode _overlayNode = null!;
  private TextDropDownNode _overlayDropdownNode = null!;
  private TextInputNode _overlayInputNode = null!;
  private TextButtonNode _overlaySubmitNode = null!;
  private TextButtonNode _overlayCancelNode = null!;
  private System.Action? _onOverlaySubmit = null;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();
    _reportService = _services.GetRequiredService<IReportService>();
    _playbackService = _services.GetRequiredService<IPlaybackService>();

    ConfigSectionNode reportSettingsSectionNode = new("Report Settings");

    reportSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      When you encounter unvoiced lines, the plugin will automatically report these.
      You have the option to opt-out of this behavior for privacy reasons.
      """
    }, inline: true);

    _enableAutomaticReportsNode = new()
    {
      String = "Enable Automatic Reports",
      Size = new Vector2(220.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.EnableAutomaticReports = value;
        _configuration.Save();
      }
    };
    reportSettingsSectionNode.AttachNode(_enableAutomaticReportsNode);

    _logReportsToChatNode = new()
    {
      String = "Print reported messages to chat",
      Size = new Vector2(260.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LogReportsToChat = value;
        _configuration.Save();
      }
    };
    reportSettingsSectionNode.AttachNode(_logReportsToChatNode);

    AttachNode(reportSettingsSectionNode);

    _audioLogsSectionNode = new("Audio Logs", reportSettingsSectionNode);

    _audioLogsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      Left-Click: Play/Pause/Skip voiceline.
      Right-Click: Report voiceline (not available for yellow LocalTTS or already-reported voicelines).
      """
    }, inline: true);

    _audioLogsNode = new()
    {
      OptionsList = [],
      OnItemSelected = (itemData, isRightClick) =>
      {
        if (isRightClick)
        {
          if (itemData.isQueued || itemData.message.IsGenerating || itemData.message.IsLocalTTS || itemData.message.Reported) return;
          _overlayInputNode.IsVisible = false;
          _overlayInputNode.String = "";
          _overlayNode.ContentNode.Height = 124.0f;
          _overlayNode.PositionContentNode();
          _overlaySubmitNode.Y = 80.0f;
          _overlayCancelNode.Y = 80.0f;
          _overlayDropdownNode.SelectedOption = "Already voiced ingame";
          _overlayNode.IsVisible = true;
          _onOverlaySubmit = () =>
          {
            string reason = "";
            if (_overlayDropdownNode.SelectedOption == "Other") reason = _overlayInputNode.String.ToString();
            else reason = _overlayDropdownNode.SelectedOption;
            if (!reason.IsNullOrWhitespace()) _reportService.ReportWithReason(itemData.message, reason);
            itemData.message.Reported = true;
          };
        }
        else
        {
          if (itemData.message.IsGenerating || itemData.isQueued)
          {
            _playbackService.SkipQueuedLine(itemData.message);
          }
          else
          {
            if (itemData.isPlaying)
              _playbackService.Stop(itemData.message.Id);
            else
              _ = _playbackService.Play(itemData.message, true);
          }
        }
      }
    };
    _audioLogsSectionNode.AttachNode(_audioLogsNode, indent: false);

    _noAudioLogsNode = new()
    {
      String = "There are no voicelines in your history.",
      Size = new Vector2(200.0f, 20.0f),
    };
    _audioLogsSectionNode.AttachNode(_noAudioLogsNode);

    AttachNode(_audioLogsSectionNode);

    _overlayNode = new ConfigOverlayNode(_services);
    _overlayNode.AttachNode(this);
    _overlayNode.Title = "Report Voiceline";

    _overlayDropdownNode = new TextDropDownNode()
    {
      Options = ["Already voiced ingame", "Mispronunciations", "Wrong Voice", "Other"],
      Size = new Vector2(300.0f, 24.0f),
      OnOptionSelected = (option) =>
      {
        if (option == "Other")
        {
          _overlayInputNode.IsVisible = true;
          _overlayNode.ContentNode.Height = 150.0f;
          _overlayNode.PositionContentNode();
          _overlaySubmitNode.Y = 105.0f;
          _overlayCancelNode.Y = 105.0f;
        }
        else
        {
          _overlayInputNode.IsVisible = false;
          _overlayNode.ContentNode.Height = 124.0f;
          _overlayNode.PositionContentNode();
          _overlaySubmitNode.Y = 80.0f;
          _overlayCancelNode.Y = 80.0f;
        }
      }
    };
    _overlayNode.AttachContent(_overlayDropdownNode);

    _overlayInputNode = new TextInputNode()
    {
      Size = new Vector2(300.0f, 30.0f),
      Position = new Vector2(0.0f, 24.0f),
      PlaceholderString = "Reason",
      IsVisible = false,
    };
    _overlayNode.AttachContent(_overlayInputNode);

    _overlaySubmitNode = new TextButtonNode
    {
      String = "Submit",
      Size = new Vector2(60.0f, 28.0f),
      Position = new Vector2(0.0f, 32.0f),
      OnClick = () =>
      {
        _onOverlaySubmit?.Invoke();
        _overlayNode.IsVisible = false;
      }
    };
    _overlayNode.AttachContent(_overlaySubmitNode);

    _overlayCancelNode = new TextButtonNode
    {
      String = "Cancel",
      Size = new Vector2(60.0f, 28.0f),
      Position = new Vector2(60.0f, 32.0f),
      OnClick = () => _overlayNode.IsVisible = false,
    };
    _overlayNode.AttachContent(_overlayCancelNode);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _overlayNode.SetSize(Size);
    _audioLogsNode.Size = new Vector2(Width, Height - _audioLogsSectionNode.Y - _audioLogsNode.Y);
  }

  public override void OnUpdate()
  {
    _audioLogsNode.OptionsList = _playbackService.GetPlaybackHistory().ToList();
    _noAudioLogsNode.IsVisible = _audioLogsNode.OptionsList.Count == 0;
  }

  public override void ConfigurationSaved()
  {
    _enableAutomaticReportsNode.IsChecked = _configuration.EnableAutomaticReports;
    _logReportsToChatNode.IsChecked = _configuration.LogReportsToChat;
  }
}
