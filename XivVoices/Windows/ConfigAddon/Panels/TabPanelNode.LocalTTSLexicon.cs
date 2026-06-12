using System.Collections.Immutable;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

using LocalTTSLexicon = (string from, string to);

public class LocalTTSLexiconTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.LocalTTSLexicon;
  private IKeyState _keyState = null!;
  private Configuration _configuration = null!;
  private IMessageDispatcher _messageDispatcher = null!;

  private ConfigSectionNode _localTTSLexiconSectionNode = null!;
  private StatelessTabBarNode _tabBarNode = null!;

  private LocalTTSModifyListNode<LocalTTSLexicon, LocalTTSLexiconItemNode> _localTTSLexiconListNode = null!;
  private Dictionary<string, string> _localTTSLexiconUndoState = [];

  private ConfigOverlayNode _lexiconOverlayNode = null!;
  private TextInputNode _lexiconOverlayFromNode = null!;
  private TextInputNode _lexiconOverlayToNode = null!;
  private TextButtonNode _lexiconOverlayApplyNode = null!;
  private System.Action? _lexiconOverlayOnApply = null;
  private string? _lexiconCurrentlyEditingFrom = null;

  public override void OnSetup()
  {
    _keyState = _services.GetRequiredService<IKeyState>();
    _configuration = _services.GetRequiredService<Configuration>();
    _messageDispatcher = _services.GetRequiredService<IMessageDispatcher>();

    _tabBarNode = new StatelessTabBarNode();
    _tabBarNode.AddTab("Settings", () => SetTab?.Invoke(ConfigTab.LocalTTSSettings));
    _tabBarNode.AddTab("Lexicon", () => SetTab?.Invoke(ConfigTab.LocalTTSLexicon), isSelected: true);
    AttachNode(_tabBarNode);

    _localTTSLexiconSectionNode = new("Local TTS Lexicon", offset: 26.0f);

    _localTTSLexiconSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      Specify text replacements for LocalTTS.  
      Use these to fix pronunciation and other word-level substitutions.

      Examples
      - "o/" -> "Hello!"
      - Regex: "/o[\/\\]/i" -> "Hello!"

      Filters
      - Use the lexicon to filter chat messages.
      - Filters can be global or per-channel.
        - Global:   "gil"    -> "_FILTER_"
        - Shout:    "hunt" -> "_SHOUT_FILTER_"
        - Multiple: "gg"   -> "_SAY_PARTY_FILTER_"

      Notes
      - Regex must be enclosed in slashes and may use only the i flag.
      - Escape backslashes in string literals (use \\).
      - Non-regex replacements apply only to whole words and are case-sensitive.
      """,
    }, inline: true);

    _localTTSLexiconListNode = new(_keyState)
    {
      AddNewEntry = () =>
      {
        _lexiconOverlayNode.Title = "Add Lexicon Entry";
        _lexiconOverlayFromNode.String = "";
        _lexiconOverlayToNode.String = "";
        _lexiconOverlayApplyNode.IsEnabled = false;
        _lexiconCurrentlyEditingFrom = null;
        _lexiconOverlayNode.IsVisible = true;
        _lexiconOverlayOnApply = () =>
        {
          _configuration.LocalTTSLexicon.Add(_lexiconOverlayFromNode.String.ToString(), _lexiconOverlayToNode.String.ToString());
          _configuration.Save();
        };
      },
      EditEntry = (data) =>
      {
        _lexiconOverlayNode.Title = "Edit Lexicon Entry";
        _lexiconOverlayFromNode.String = data.from;
        _lexiconOverlayToNode.String = data.to;
        _lexiconOverlayApplyNode.IsEnabled = true;
        _lexiconCurrentlyEditingFrom = data.from;
        _lexiconOverlayNode.IsVisible = true;
        _lexiconOverlayOnApply = () =>
        {
          _configuration.LocalTTSLexicon.Remove(_lexiconCurrentlyEditingFrom);
          _configuration.LocalTTSLexicon.Add(_lexiconOverlayFromNode.String.ToString(), _lexiconOverlayToNode.String.ToString());
          _configuration.Save();
        };
      },
      RemoveEntry = (data) =>
      {
        _configuration.LocalTTSLexicon.Remove(data.from);
        _configuration.Save();
      },
      OnImport = (shouldOverride) =>
      {
        _localTTSLexiconUndoState = new Dictionary<string, string>(_configuration.LocalTTSLexicon);

        Dictionary<string, string>? localTTSLexicon = _configuration.DeserializeFromBase64<Dictionary<string, string>>(ImGui.GetClipboardText());
        if (localTTSLexicon == null || localTTSLexicon.Count == 0) return ("Nothing found to Import.", false);

        int replaced = 0;
        int done = 0;
        foreach (KeyValuePair<string, string> kvp in localTTSLexicon)
        {
          if (_configuration.LocalTTSLexicon.TryGetValue(kvp.Key, out string? oldValue))
          {
            if (oldValue != kvp.Value)
            {
              if (!shouldOverride) continue;
              replaced++;
            }
            else
            {
              continue;
            }
          }
          _configuration.LocalTTSLexicon[kvp.Key] = kvp.Value;
          done++;
        }

        if (done == 0) return ("Nothing found to Import.", false);

        _configuration.Save();
        string str = replaced == 1 ? "override" : "overrides";
        return (replaced > 0 ? $"Imported ({replaced} {str})." : "Successfully Imported!", true);
      },
      OnExport = () =>
      {
        ImGui.SetClipboardText(_configuration.SerializeToBase64(_configuration.LocalTTSLexicon));
        return ("Copied to Clipboard!", true);
      },
      OnUndo = () =>
      {
        _configuration.LocalTTSLexicon = _localTTSLexiconUndoState;
        _configuration.Save();
        return ("Import undone.", true);
      },
      ItemComparer = (left, right, mode) => left.from.CompareTo(right.from),
      IsSearchMatch = (data, search) => data.from.Contains(search, StringComparison.OrdinalIgnoreCase) || data.to.Contains(search, StringComparison.OrdinalIgnoreCase),
    };

    _localTTSLexiconSectionNode.AttachNode(_localTTSLexiconListNode, indent: false, padding: -4.0f);

    AttachNode(_localTTSLexiconSectionNode);

    _lexiconOverlayNode = new ConfigOverlayNode(_services);
    _lexiconOverlayNode.AttachNode(this);

    _lexiconOverlayFromNode = new()
    {
      Size = new Vector2(145.0f, 30.0f),
      PlaceholderString = "From",
      OnInputReceived = (str) =>
      {
        _lexiconOverlayApplyNode.IsEnabled =
          !str.IsEmpty &&
          (str == _lexiconCurrentlyEditingFrom
          || !_configuration.LocalTTSLexicon.ContainsKey(str.ToString()));
      }
    };
    _lexiconOverlayNode.AttachContent(_lexiconOverlayFromNode);

    _lexiconOverlayToNode = new()
    {
      Position = new Vector2(145.0f, 0.0f),
      Size = new Vector2(145.0f, 30.0f),
      PlaceholderString = "To"
    };
    _lexiconOverlayNode.AttachContent(_lexiconOverlayToNode);

    CircleButtonNode overrideOverlayPreviewNode = new()
    {
      Position = new Vector2(_lexiconOverlayToNode.Bounds.Right - 12.0f, 2.0f),
      Size = new Vector2(28.0f, 28.0f),
      Icon = ButtonIcon.RightArrow,
      TextTooltip = "Preview",
      OnClick = () =>
      {
        string sentence = _lexiconOverlayToNode.String.IsEmpty ? "You can't preview an empty replacement" : _lexiconOverlayToNode.String.ToString();
        _messageDispatcher.DispatchLocalTTSMessage("Heart", 100, sentence);
      }
    };
    _lexiconOverlayNode.AttachContent(overrideOverlayPreviewNode);

    _lexiconOverlayNode.AttachContent(new HorizontalLineNode()
    {
      Position = new Vector2(0.0f, _lexiconOverlayToNode.Bounds.Bottom - 48.0f),
      Size = new Vector2(320.0f, 4.0f),
    });

    _lexiconOverlayApplyNode = new TextButtonNode
    {
      String = "Apply",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(75.0f, _lexiconOverlayToNode.Bounds.Bottom - 48.0f + 8.0f),
      OnClick = () =>
      {
        _lexiconOverlayNode.IsVisible = false;
        _lexiconOverlayOnApply?.Invoke();
      }
    };
    _lexiconOverlayNode.AttachContent(_lexiconOverlayApplyNode);

    _lexiconOverlayNode.AttachContent(new TextButtonNode
    {
      String = "Close",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(195.0f, _lexiconOverlayToNode.Bounds.Bottom - 48.0f + 8.0f),
      OnClick = () =>
      {
        _lexiconOverlayNode.IsVisible = false;
      }
    });
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _tabBarNode.Size = new Vector2(Width, 24.0f);
    _lexiconOverlayNode.SetSize(Size);
    _localTTSLexiconListNode.Size = new Vector2(Width, Height - _localTTSLexiconSectionNode.Y - _localTTSLexiconListNode.Y);
  }

  public override void ConfigurationSaved()
  {
    _localTTSLexiconListNode.Options = _configuration.LocalTTSLexicon.Select(kv => (kv.Key, kv.Value)).ToList();
    _localTTSLexiconListNode.RefreshList();
  }

  public override void OnUpdate()
  {
    _localTTSLexiconListNode.OnUpdate();
  }
}
