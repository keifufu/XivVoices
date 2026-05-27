using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

using LocalTTSOverride = (string speaker, (string voice, int pitch) options);

public class LocalTTSSettingsTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.LocalTTSSettings;
  private Configuration _configuration = null!;
  private ILocalTTSService _localTTSService = null!;
  private IMessageDispatcher _messageDispatcher = null!;
  private IGameInteropService _gameInteropService = null!;

  private StatelessTabBarNode _tabBarNode = null!;

  private TextDropDownNode _localTTSDefaultVoiceNode = null!;
  private TextDropDownNode _localTTSMaleVoiceNode = null!;
  private TextDropDownNode _localTTSFemaleVoiceNode = null!;

  private CheckboxNode _localTTSVoiceRandomizationNode = null!;
  private CheckboxNode _localTTSPitchRandomizationNode = null!;

  private ConfigSectionNode _localTTSOverridesSectionNode = null!;
  private LocalTTSModifyListNode<LocalTTSOverride, LocalTTSOverrideItemNode> _localTTSOverridesListNode = null!;

  private ConfigOverlayNode _overrideOverlayNode = null!;
  private TextInputNode _overrideOverlaySpeakerNode = null!;
  private TextDropDownNode _overrideOverlayVoiceNode = null!;
  private SliderNode _overrideOverlayPitchNode = null!;
  private TextButtonNode _overrideOverlayApplyNode = null!;
  private System.Action? _overrideOverlayOnApply = null;
  private string? _overrideCurrentlyEditingSpeaker = null;

  private ConfigOverlayNode _allowedVoicesOverlayNode = null!;
  private ScrollingAreaNode<ResNode> _allowedVoicesContainerNode = null!;
  private readonly List<CheckboxNode> _allowedVoicesMaleCheckboxNodes = [];
  private readonly List<CircleButtonNode> _allowedVoicesMalePreviewNodes = [];
  private readonly List<CheckboxNode> _allowedVoicesFemaleCheckboxNodes = [];
  private readonly List<CircleButtonNode> _allowedVoicesFemalePreviewNodes = [];
  private LabelTextNode _allowedVoicesMaleLabelNode = null!;
  private LabelTextNode _allowedVoicesFemaleLabelNode = null!;
  private TextButtonNode _allowedVoicesApplyNode = null!;
  private System.Action? _allowedVoicesOnApply = null;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();
    _localTTSService = _services.GetRequiredService<ILocalTTSService>();
    _messageDispatcher = _services.GetRequiredService<IMessageDispatcher>();
    _gameInteropService = _services.GetRequiredService<IGameInteropService>();

    _localTTSService.OnInitialized += ConfigurationSaved;

    _tabBarNode = new();
    _tabBarNode.AddTab("Settings", () => SetTab?.Invoke(ConfigTab.LocalTTSSettings), isSelected: true);
    _tabBarNode.AddTab("Lexicon", () => SetTab?.Invoke(ConfigTab.LocalTTSLexicon));
    AttachNode(_tabBarNode);

    ConfigSectionNode defaultSettingsSectionNode = new("Default Settings", offset: 26.0f);

    defaultSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      Used when randomization is disabled or when the gender could not be determined.
      Please note that a players voice, if not overridden, will only reflect their correct gender once you have seen their character at least once.
      """,
    }, inline: true);

    defaultSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Default Voice",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 2.0f);

    _localTTSDefaultVoiceNode = new TextDropDownNode()
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
    defaultSettingsSectionNode.AttachNode(_localTTSDefaultVoiceNode, inline: true, padding: -2.0f);

    defaultSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Male Voice",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 6.0f);

    _localTTSMaleVoiceNode = new TextDropDownNode()
    {
      PlaceholderString = "No voices available",
      X = 140.0f,
      Size = new Vector2(195.0f, 24.0f),
      OnOptionSelected = (option) =>
      {
        _configuration.LocalTTSMaleVoice = option;
        _configuration.Save();
      }
    };
    defaultSettingsSectionNode.AttachNode(_localTTSMaleVoiceNode, inline: true, padding: -2.0f);

    defaultSettingsSectionNode.AttachNode(new CircleButtonNode
    {
      Size = new Vector2(30.0f, 30.0f),
      Icon = ButtonIcon.RightArrow,
      TextTooltip = "Preview",
      X = 330.0f,
      OnClick = () =>
      {
        _messageDispatcher.DispatchLocalTTSMessage(_configuration.LocalTTSMaleVoice, 100, $"This is a preview of {_configuration.LocalTTSMaleVoice}.");
      }
    }, inline: true, padding: -5.0f);

    defaultSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Female Voice",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 6.0f);

    _localTTSFemaleVoiceNode = new TextDropDownNode()
    {
      PlaceholderString = "No voices available",
      X = 140.0f,
      Size = new Vector2(195.0f, 24.0f),
      OnOptionSelected = (option) =>
      {
        _configuration.LocalTTSFemaleVoice = option;
        _configuration.Save();
      }
    };
    defaultSettingsSectionNode.AttachNode(_localTTSFemaleVoiceNode, inline: true, padding: -2.0f);

    defaultSettingsSectionNode.AttachNode(new CircleButtonNode
    {
      Size = new Vector2(30.0f, 30.0f),
      Icon = ButtonIcon.RightArrow,
      TextTooltip = "Preview",
      X = 330.0f,
      OnClick = () =>
      {
        _messageDispatcher.DispatchLocalTTSMessage(_configuration.LocalTTSFemaleVoice, 100, $"This is a preview of {_configuration.LocalTTSFemaleVoice}.");
      }
    }, inline: true, padding: -5.0f);

    AttachNode(defaultSettingsSectionNode);

    ConfigSectionNode randomizationSettingsSectionNode = new("Randomization Settings", defaultSettingsSectionNode);

    randomizationSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      NPCs and Players will get a random but persistent voice and or pitch selected.
      If disabled, the default voice for the associated gender will be used.
      Specific voices can be selected for randomization under "Allowed Voices".
      """,
    }, inline: true);

    _localTTSVoiceRandomizationNode = new CheckboxNode
    {
      String = "Randomize Voices",
      Size = new Vector2(160.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSVoiceRandomization = value;
        _configuration.Save();
      }
    };
    randomizationSettingsSectionNode.AttachNode(_localTTSVoiceRandomizationNode);

    _localTTSPitchRandomizationNode = new CheckboxNode
    {
      String = "Randomize Pitch",
      Size = new Vector2(150.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSPitchRandomization = value;
        _configuration.Save();
      }
    };
    randomizationSettingsSectionNode.AttachNode(_localTTSPitchRandomizationNode);

    AttachNode(randomizationSettingsSectionNode);

    ConfigSectionNode allowedVoicesSectionNode = new("Allowed Voices", randomizationSettingsSectionNode);

    allowedVoicesSectionNode.AttachNode(new TextButtonNode
    {
      String = "Allowed Voices",
      X = 140.0f,
      Size = new Vector2(220.0f, 28.0f),
      OnClick = () =>
      {
        _allowedVoicesOverlayNode.IsVisible = true;
        _allowedVoicesOnApply = () =>
        {
          _configuration.LocalTTSDisallowedVoices.Clear();
          foreach (CheckboxNode node in _allowedVoicesMaleCheckboxNodes)
            if (!node.IsChecked && node.IsVisible) _configuration.LocalTTSDisallowedVoices.Add(node.String.ToString());
          foreach (CheckboxNode node in _allowedVoicesFemaleCheckboxNodes)
            if (!node.IsChecked && node.IsVisible) _configuration.LocalTTSDisallowedVoices.Add(node.String.ToString());
          _configuration.Save();
        };
      }
    }, inline: true);

    AttachNode(allowedVoicesSectionNode);

    _localTTSOverridesSectionNode = new("Voice and Pitch Overrides", allowedVoicesSectionNode);

    _localTTSOverridesSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      NPC or Player specific overrides for voice and pitch.
      Name entry is case-insensitive and includes world names.
      Example: "Character Name@World"
      """,
    }, inline: true);

    _localTTSOverridesListNode = new()
    {
      AddNewEntry = () =>
      {
        _overrideOverlayNode.Title = "Add Override";
        _overrideOverlaySpeakerNode.String = "";
        _overrideOverlayVoiceNode.SelectedOption = _overrideOverlayVoiceNode.Options[0];
        _overrideOverlayPitchNode.Value = 100;
        _overrideOverlayApplyNode.IsEnabled = false;
        _overrideCurrentlyEditingSpeaker = null;
        _overrideOverlayNode.IsVisible = true;
        _overrideOverlayOnApply = () =>
        {
          _configuration.LocalTTSOverrides.Add(_overrideOverlaySpeakerNode.String.ToString(), (_overrideOverlayVoiceNode.SelectedOption, _overrideOverlayPitchNode.Value));
          _configuration.Save();
        };
      },
      EditEntry = (data) =>
      {
        _overrideOverlayNode.Title = "Edit Override";
        _overrideOverlaySpeakerNode.String = data.speaker;
        _overrideOverlayVoiceNode.SelectedOption = data.options.voice;
        _overrideOverlayPitchNode.Value = data.options.pitch;
        _overrideOverlayApplyNode.IsEnabled = true;
        _overrideCurrentlyEditingSpeaker = data.speaker;
        _overrideOverlayNode.IsVisible = true;
        _overrideOverlayOnApply = () =>
        {
          _configuration.LocalTTSOverrides.Remove(_overrideCurrentlyEditingSpeaker);
          _configuration.LocalTTSOverrides.Add(_overrideOverlaySpeakerNode.String.ToString(), (_overrideOverlayVoiceNode.SelectedOption, _overrideOverlayPitchNode.Value));
          _configuration.Save();
        };
      },
      RemoveEntry = (data) =>
      {
        _configuration.LocalTTSOverrides.Remove(data.speaker);
        _configuration.Save();
      },
      ItemComparer = (left, right, mode) => left.speaker.CompareTo(right.speaker),
      IsSearchMatch = (data, search) => data.speaker.Contains(search, StringComparison.OrdinalIgnoreCase),
    };
    _localTTSOverridesSectionNode.AttachNode(_localTTSOverridesListNode, indent: false, padding: -4.0f);

    AttachNode(_localTTSOverridesSectionNode);

    _overrideOverlayNode = new ConfigOverlayNode(_services);
    _overrideOverlayNode.AttachNode(this);

    _overrideOverlaySpeakerNode = new()
    {
      Size = new Vector2(320.0f, 30.0f),
      PlaceholderString = "NPC or Player Name",
      OnInputReceived = (str) =>
      {
        _overrideOverlayApplyNode.IsEnabled =
          !str.IsEmpty &&
          (str == _overrideCurrentlyEditingSpeaker
          || !_configuration.LocalTTSOverrides.ContainsKey(str.ToString()));
      }
    };
    _overrideOverlayNode.AttachContent(_overrideOverlaySpeakerNode);

    _overrideOverlayVoiceNode = new()
    {
      Size = new Vector2(135.0f, 24.0f),
      Position = new Vector2(0.0f, _overrideOverlaySpeakerNode.Bounds.Bottom - 48.0f)
    };
    _overrideOverlayNode.AttachContent(_overrideOverlayVoiceNode);

    CircleButtonNode overrideOverlayPreviewNode = new()
    {
      Position = new Vector2(_overrideOverlayVoiceNode.Bounds.Right - 12.0f, _overrideOverlaySpeakerNode.Bounds.Bottom - 48.0f - 3.0f),
      Size = new Vector2(28.0f, 28.0f),
      Icon = ButtonIcon.RightArrow,
      TextTooltip = "Preview",
      OnClick = () =>
      {
        string? voice = _overrideOverlayVoiceNode.SelectedOption;
        if (voice != null) _messageDispatcher.DispatchLocalTTSMessage(voice, _overrideOverlayPitchNode.Value, $"This is a preview of {voice}.");
      }
    };
    _overrideOverlayNode.AttachContent(overrideOverlayPreviewNode);

    _overrideOverlayPitchNode = new()
    {
      Range = 50..150,
      Size = new Vector2(160.0f, 16.0f),
      Position = new Vector2(overrideOverlayPreviewNode.Bounds.Right - 8.0f, _overrideOverlaySpeakerNode.Bounds.Bottom - 48.0f + 3.0f)
    };
    _overrideOverlayNode.AttachContent(_overrideOverlayPitchNode);

    _overrideOverlayNode.AttachContent(new HorizontalLineNode()
    {
      Position = new Vector2(0.0f, _overrideOverlayVoiceNode.Bounds.Bottom - 48.0f),
      Size = new Vector2(320.0f, 4.0f),
    });

    _overrideOverlayApplyNode = new TextButtonNode
    {
      String = "Apply",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(75.0f, _overrideOverlayVoiceNode.Bounds.Bottom - 48.0f + 8.0f),
      OnClick = () =>
      {
        _overrideOverlayNode.IsVisible = false;
        _overrideOverlayOnApply?.Invoke();
      }
    };
    _overrideOverlayNode.AttachContent(_overrideOverlayApplyNode);

    _overrideOverlayNode.AttachContent(new TextButtonNode
    {
      String = "Close",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(195.0f, _overrideOverlayVoiceNode.Bounds.Bottom - 48.0f + 8.0f),
      OnClick = () =>
      {
        _overrideOverlayNode.IsVisible = false;
      }
    });

    _allowedVoicesOverlayNode = new ConfigOverlayNode(_services);
    _allowedVoicesOverlayNode.AttachNode(this);
    _allowedVoicesOverlayNode.Title = "Allowed Voices";

    _allowedVoicesContainerNode = new()
    {
      ContentHeight = 0.0f,
      AutoHideScrollBar = true,
      Size = new Vector2(300.0f, 345.0f),
    };
    _allowedVoicesOverlayNode.AttachContent(_allowedVoicesContainerNode);

    _allowedVoicesMaleLabelNode = new()
    {
      String = "Male Voices",
      Size = new Vector2(200.0f, 20.0f),
      TextColor = ColorHelper.GetColor(2),
    };
    _allowedVoicesMaleLabelNode.AttachNode(_allowedVoicesContainerNode.ContentNode);

    for (int i = 0; i < 25; i++)
    {
      CheckboxNode checkboxNode = new()
      {
        Height = 24.0f,
      };
      checkboxNode.AttachNode(_allowedVoicesContainerNode.ContentNode);
      _allowedVoicesMaleCheckboxNodes.Add(checkboxNode);

      CircleButtonNode previewNode = new()
      {
        Size = new Vector2(28.0f, 28.0f),
        Icon = ButtonIcon.RightArrow,
        TextTooltip = "Preview",
      };
      previewNode.AttachNode(_allowedVoicesContainerNode.ContentNode);
      _allowedVoicesMalePreviewNodes.Add(previewNode);
    }

    _allowedVoicesFemaleLabelNode = new()
    {
      String = "Female Voices",
      Size = new Vector2(200.0f, 20.0f),
      TextColor = ColorHelper.GetColor(2),
    };
    _allowedVoicesFemaleLabelNode.AttachNode(_allowedVoicesContainerNode.ContentNode);

    for (int i = 0; i < 25; i++)
    {
      CheckboxNode checkboxNode = new()
      {
        Height = 24.0f,
      };
      checkboxNode.AttachNode(_allowedVoicesContainerNode.ContentNode);
      _allowedVoicesFemaleCheckboxNodes.Add(checkboxNode);

      CircleButtonNode previewNode = new()
      {
        Size = new Vector2(28.0f, 28.0f),
        Icon = ButtonIcon.RightArrow,
        TextTooltip = "Preview",
      };
      previewNode.AttachNode(_allowedVoicesContainerNode.ContentNode);
      _allowedVoicesFemalePreviewNodes.Add(previewNode);
    }

    _allowedVoicesOverlayNode.AttachContent(new HorizontalLineNode()
    {
      Position = new Vector2(0.0f, _allowedVoicesContainerNode.Height),
      Size = new Vector2(320.0f, 4.0f),
    });

    _allowedVoicesApplyNode = new TextButtonNode
    {
      String = "Apply",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(75.0f, _allowedVoicesContainerNode.Height + 8.0f),
      OnClick = () =>
      {
        _allowedVoicesOverlayNode.IsVisible = false;
        _allowedVoicesOnApply?.Invoke();
      }
    };
    _allowedVoicesOverlayNode.AttachContent(_allowedVoicesApplyNode);

    _allowedVoicesOverlayNode.AttachContent(new TextButtonNode
    {
      String = "Close",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(195.0f, _allowedVoicesContainerNode.Height + 8.0f),
      OnClick = () =>
      {
        _allowedVoicesOverlayNode.IsVisible = false;
        ConfigurationSaved(); // Update the checkboxes
      }
    });
  }

  protected override void Dispose(bool disposing, bool isNativeDestructor)
  {
    base.Dispose(disposing, isNativeDestructor);

    _localTTSService.OnInitialized -= ConfigurationSaved;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _tabBarNode.Size = new Vector2(Width, 24.0f);
    _overrideOverlayNode.SetSize(Size);
    _allowedVoicesOverlayNode.SetSize(Size);
    NativeUtils.FixSliderNode(_overrideOverlayPitchNode);
    _localTTSOverridesListNode.Size = new Vector2(Width, Height - _localTTSOverridesSectionNode.Y - _localTTSOverridesListNode.Y);
  }

  public override void ConfigurationSaved()
  {
    if (!SetupComplete) return;

    List<string> maleVoices = _localTTSService.Voices
      .Where(v => v.Gender == "Male")
      .Select(v => v.Name)
      .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
      .ToList();

    List<string> femaleVoices = _localTTSService.Voices
      .Where(v => v.Gender == "Female")
      .Select(v => v.Name)
      .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
      .ToList();

    _gameInteropService.RunOnFrameworkThread(() =>
    {
      _localTTSDefaultVoiceNode.SelectedOption = _configuration.LocalTTSDefaultVoice;

      _localTTSMaleVoiceNode.Options = maleVoices;
      _localTTSMaleVoiceNode.SelectedOption = _configuration.LocalTTSMaleVoice;

      _localTTSFemaleVoiceNode.Options = femaleVoices;
      _localTTSFemaleVoiceNode.SelectedOption = _configuration.LocalTTSFemaleVoice;

      _localTTSVoiceRandomizationNode.IsChecked = _configuration.LocalTTSVoiceRandomization;
      _localTTSPitchRandomizationNode.IsChecked = _configuration.LocalTTSPitchRandomization;

      _localTTSOverridesListNode.Options = _configuration.LocalTTSOverrides.Select(kv => (kv.Key, kv.Value)).ToList();
      _localTTSOverridesListNode.RefreshList();

      _overrideOverlayVoiceNode.Options = maleVoices.Concat(femaleVoices).ToList();

      _allowedVoicesApplyNode.IsEnabled = false;
      _allowedVoicesContainerNode.ContentHeight = 0.0f;
      _allowedVoicesMaleLabelNode.Y = _allowedVoicesContainerNode.ContentHeight;
      _allowedVoicesMaleLabelNode.X = 8.0f;
      _allowedVoicesContainerNode.ContentHeight = _allowedVoicesMaleLabelNode.Y + _allowedVoicesMaleLabelNode.Height + 5.0f;
      for (int i = 0; i < _allowedVoicesMaleCheckboxNodes.Count; i++)
      {
        CheckboxNode checkboxNode = _allowedVoicesMaleCheckboxNodes[i];
        CircleButtonNode previewNode = _allowedVoicesMalePreviewNodes[i];
        if (i >= maleVoices.Count)
        {
          checkboxNode.IsVisible = false;
          previewNode.IsVisible = false;
          continue;
        }
        string voice = maleVoices[i];
        checkboxNode.IsVisible = true;
        previewNode.IsVisible = true;
        checkboxNode.String = voice;
        checkboxNode.IsChecked = !_configuration.LocalTTSDisallowedVoices.Contains(voice);
        checkboxNode.OnClick = (_) => _allowedVoicesApplyNode.IsEnabled = true;
        checkboxNode.Y = _allowedVoicesContainerNode.ContentHeight;
        checkboxNode.X = 8.0f;
        previewNode.Y = _allowedVoicesContainerNode.ContentHeight - 2.0f;
        previewNode.X = 100.0f;
        previewNode.OnClick = () => _messageDispatcher.DispatchLocalTTSMessage(voice, 100, $"This is a preview of {voice}.");
        _allowedVoicesContainerNode.ContentHeight = checkboxNode.Y + checkboxNode.Height + 5.0f;
      }

      float prevContentHeight = _allowedVoicesContainerNode.ContentHeight;
      _allowedVoicesContainerNode.ContentHeight = 0.0f;
      _allowedVoicesFemaleLabelNode.Y = _allowedVoicesContainerNode.ContentHeight;
      _allowedVoicesFemaleLabelNode.X = 150.0f;
      _allowedVoicesContainerNode.ContentHeight = _allowedVoicesFemaleLabelNode.Y + _allowedVoicesFemaleLabelNode.Height + 5.0f;
      for (int i = 0; i < _allowedVoicesFemaleCheckboxNodes.Count; i++)
      {
        CheckboxNode checkboxNode = _allowedVoicesFemaleCheckboxNodes[i];
        CircleButtonNode previewNode = _allowedVoicesFemalePreviewNodes[i];
        if (i >= femaleVoices.Count)
        {
          checkboxNode.IsVisible = false;
          previewNode.IsVisible = false;
          continue;
        }
        string voice = femaleVoices[i];
        checkboxNode.IsVisible = true;
        previewNode.IsVisible = true;
        checkboxNode.String = voice;
        checkboxNode.IsChecked = !_configuration.LocalTTSDisallowedVoices.Contains(voice);
        checkboxNode.OnClick = (_) => _allowedVoicesApplyNode.IsEnabled = true;
        checkboxNode.Y = _allowedVoicesContainerNode.ContentHeight;
        checkboxNode.X = 150.0f;
        previewNode.Y = _allowedVoicesContainerNode.ContentHeight - 2.0f;
        previewNode.X = 250.0f;
        previewNode.OnClick = () => _messageDispatcher.DispatchLocalTTSMessage(voice, 100, $"This is a preview of {voice}.");
        _allowedVoicesContainerNode.ContentHeight = checkboxNode.Y + checkboxNode.Height + 5.0f;
      }

      if (prevContentHeight > _allowedVoicesContainerNode.ContentHeight)
        _allowedVoicesContainerNode.ContentHeight = prevContentHeight;
    });
  }
}
