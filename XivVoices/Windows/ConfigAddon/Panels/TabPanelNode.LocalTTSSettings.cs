using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

using LocalTTSOverride = (string speaker, (string voice, int pitch) options);

public class LocalTTSSettingsTabPanelNode(IServiceProvider _services) : TabPanelNode
{
  public override ConfigTab Tab => ConfigTab.LocalTTSSettings;
  private IKeyState _keyState = null!;
  private Configuration _configuration = null!;
  private ILocalTTSService _localTTSService = null!;
  private IMessageDispatcher _messageDispatcher = null!;
  private IGameInteropService _gameInteropService = null!;

  private StatelessTabBarNode _tabBarNode = null!;

  private StringDropDownNode _localTTSDefaultVoiceNode = null!;
  private StringDropDownNode _localTTSMaleVoiceNode = null!;
  private StringDropDownNode _localTTSFemaleVoiceNode = null!;
  private StringDropDownNode _localTTSCPUUsageNode = null!;

  private CheckboxNode _localTTSVoiceRandomizationNode = null!;
  private CheckboxNode _localTTSPitchRandomizationNode = null!;

  private ConfigSectionNode _localTTSOverridesSectionNode = null!;
  private ModifyListNode<LocalTTSOverride, LocalTTSOverrideItemNode> _localTTSOverridesListNode = null!;
  private Dictionary<string, (string voice, int pitch)> _localTTSOverridesUndoState = [];

  private ConfigOverlayNode _overrideOverlayNode = null!;
  private TextInputNode _overrideOverlaySpeakerNode = null!;
  private StringDropDownNode _overrideOverlayVoiceNode = null!;
  private SliderNode _overrideOverlayPitchNode = null!;
  private TextButtonNode _overrideOverlayApplyNode = null!;
  private System.Action? _overrideOverlayOnApply = null;
  private string? _overrideCurrentlyEditingSpeaker = null;

  private ConfigOverlayNode _chatChannelVoicesOverlayNode = null!;
  private CheckboxNode _chatChannelVoicesEnabledNode = null!;
  private Dictionary<XivChatType, (StringDropDownNode maleNode, StringDropDownNode femaleNode)> _chatChannelVoicesNodes = [];
  private TextButtonNode _chatChannelVoicesApplyNode = null!;
  private System.Action? _chatChannelVoicesOnApply = null;

  private ConfigOverlayNode _allowedVoicesOverlayNode = null!;
  private ScrollingNode<ResNode> _allowedVoicesContainerNode = null!;
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
    _keyState = _services.GetRequiredService<IKeyState>();
    _configuration = _services.GetRequiredService<Configuration>();
    _localTTSService = _services.GetRequiredService<ILocalTTSService>();
    _messageDispatcher = _services.GetRequiredService<IMessageDispatcher>();
    _gameInteropService = _services.GetRequiredService<IGameInteropService>();

    _tabBarNode = new();
    _tabBarNode.AddTab("Settings", () => SetTab?.Invoke(ConfigTab.LocalTTSSettings), isSelected: true);
    _tabBarNode.AddTab("Lexicon", () => SetTab?.Invoke(ConfigTab.LocalTTSLexicon));
    _tabBarNode.AddTab("Advanced", () => SetTab?.Invoke(ConfigTab.LocalTTSAdvanced));
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

    _localTTSDefaultVoiceNode = new StringDropDownNode()
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

    _localTTSMaleVoiceNode = new StringDropDownNode()
    {
      PlaceholderString = "No voices available",
      X = 140.0f,
      Size = new Vector2(195.0f, 24.0f),
      MaxListOptions = 8,
      OnOptionSelected = (option) =>
      {
        if (option.Contains("==="))
        {
          // Invalid selection, revert back.
          Task.Run(ConfigurationSaved);
          return;
        }

        _configuration.LocalTTSMaleVoice = option;
        _configuration.Save();
      }
    };
    defaultSettingsSectionNode.AttachNode(_localTTSMaleVoiceNode, inline: true, padding: -2.0f);

    defaultSettingsSectionNode.AttachNode(new CircleButtonNode
    {
      Size = new Vector2(30.0f, 30.0f),
      Icon = CircleButtonIcon.RightArrow,
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

    _localTTSFemaleVoiceNode = new StringDropDownNode()
    {
      PlaceholderString = "No voices available",
      X = 140.0f,
      Size = new Vector2(195.0f, 24.0f),
      MaxListOptions = 8,
      OnOptionSelected = (option) =>
      {
        if (option.Contains("==="))
        {
          // Invalid selection, revert back.
          Task.Run(ConfigurationSaved);
          return;
        }

        _configuration.LocalTTSFemaleVoice = option;
        _configuration.Save();
      }
    };
    defaultSettingsSectionNode.AttachNode(_localTTSFemaleVoiceNode, inline: true, padding: -2.0f);

    defaultSettingsSectionNode.AttachNode(new CircleButtonNode
    {
      Size = new Vector2(30.0f, 30.0f),
      Icon = CircleButtonIcon.RightArrow,
      TextTooltip = "Preview",
      X = 330.0f,
      OnClick = () =>
      {
        _messageDispatcher.DispatchLocalTTSMessage(_configuration.LocalTTSFemaleVoice, 100, $"This is a preview of {_configuration.LocalTTSFemaleVoice}.");
      }
    }, inline: true, padding: -5.0f);

    defaultSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "CPU Usage",
      Height = 18.0f,
      FontSize = 14,
    }, padding: 6.0f);

    _localTTSCPUUsageNode = new StringDropDownNode()
    {
      Options = ["Low", "Medium", "High"],
      X = 140.0f,
      Size = new Vector2(220.0f, 24.0f),
      OnOptionSelected = (option) =>
      {
        _configuration.LocalTTSThreads = option switch
        {
          "Low" => 2,
          "Medium" => 4,
          "High" => 8,
          _ => _configuration.LocalTTSThreads
        };
        _configuration.Save();
        Task.Run(_localTTSService.Reinitialize);
      }
    };
    defaultSettingsSectionNode.AttachNode(_localTTSCPUUsageNode, inline: true, padding: -2.0f);

    AttachNode(defaultSettingsSectionNode);

    ConfigSectionNode chatChannelsSectionNode = new("Chat Channels", defaultSettingsSectionNode);

    chatChannelsSectionNode.AttachNode(new TextButtonNode
    {
      String = "Chat Channels",
      X = 140.0f,
      Size = new Vector2(220.0f, 28.0f),
      OnClick = () =>
      {
        _chatChannelVoicesOverlayNode.IsVisible = true;
        _chatChannelVoicesOnApply = () =>
        {
          _configuration.LocalTTSChatChannelVoicesEnabled = _chatChannelVoicesEnabledNode.IsChecked;
          _configuration.LocalTTSChatChannelVoices.Clear();

          foreach (KeyValuePair<XivChatType, (StringDropDownNode maleNode, StringDropDownNode femaleNode)> kvp in _chatChannelVoicesNodes)
          {
            string? maleVoice = kvp.Value.maleNode.SelectedOption;
            if (maleVoice == "Default Male") maleVoice = null;
            if (maleVoice?.Contains("===") ?? false) maleVoice = null;

            string? femaleVoice = kvp.Value.femaleNode.SelectedOption;
            if (femaleVoice == "Default Female") femaleVoice = null;
            if (femaleVoice?.Contains("===") ?? false) maleVoice = null;

            if (maleVoice == null && femaleVoice == null) continue;
            _configuration.LocalTTSChatChannelVoices.Add(kvp.Key, (maleVoice, femaleVoice));
          }

          _configuration.Save();
        };
      }
    }, inline: true);

    AttachNode(chatChannelsSectionNode);

    ConfigSectionNode randomizationSettingsSectionNode = new("Randomization Settings", chatChannelsSectionNode, offset: -4.0f);

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

    _localTTSOverridesListNode = new(_keyState)
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
      OnImport = (shouldOverride) =>
      {
        _localTTSOverridesUndoState = new Dictionary<string, (string voice, int pitch)>(_configuration.LocalTTSOverrides);

        Dictionary<string, (string voice, int pitch)>? localTTSOverrides = _configuration.DeserializeFromBase64<Dictionary<string, (string voice, int pitch)>>(ImGui.GetClipboardText());
        if (localTTSOverrides == null || localTTSOverrides.Count == 0) return ("Nothing found to Import.", false);

        int replaced = 0;
        int done = 0;
        foreach (KeyValuePair<string, (string voice, int pitch)> kvp in localTTSOverrides)
        {
          if (_configuration.LocalTTSOverrides.TryGetValue(kvp.Key, out (string voice, int pitch) oldValue))
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
          _configuration.LocalTTSOverrides[kvp.Key] = kvp.Value;
          done++;
        }

        if (done == 0) return ("Nothing found to Import.", false);

        _configuration.Save();
        string str = replaced == 1 ? "override" : "overrides";
        return (replaced > 0 ? $"Imported ({replaced} {str})." : "Successfully Imported!", true);
      },
      OnExport = () =>
      {
        ImGui.SetClipboardText(_configuration.SerializeToBase64(_configuration.LocalTTSOverrides));
        return ("Copied to Clipboard!", true);
      },
      OnUndo = () =>
      {
        _configuration.LocalTTSOverrides = _localTTSOverridesUndoState;
        _configuration.Save();
        return ("Import undone.", true);
      },
      ItemComparer = (left, right) => left.speaker.CompareTo(right.speaker),
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
      Icon = CircleButtonIcon.RightArrow,
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

    _chatChannelVoicesOverlayNode = new ConfigOverlayNode(_services);
    _chatChannelVoicesOverlayNode.AttachNode(this);
    _chatChannelVoicesOverlayNode.Title = "Chat Channels";

    List<XivChatType> chatTypes = [
      XivChatType.Say,
      XivChatType.TellIncoming,
      XivChatType.TellOutgoing,
      XivChatType.Party,
      XivChatType.CrossParty,
      XivChatType.Shout,
      XivChatType.Yell,
      XivChatType.Alliance,
      XivChatType.FreeCompany,
      XivChatType.CustomEmote,
      XivChatType.StandardEmote,
      XivChatType.CrossLinkShell1,
      XivChatType.CrossLinkShell2,
      XivChatType.CrossLinkShell3,
      XivChatType.CrossLinkShell4,
      XivChatType.CrossLinkShell5,
      XivChatType.CrossLinkShell6,
      XivChatType.CrossLinkShell7,
      XivChatType.CrossLinkShell8,
      XivChatType.Ls1,
      XivChatType.Ls2,
      XivChatType.Ls3,
      XivChatType.Ls4,
      XivChatType.Ls5,
      XivChatType.Ls6,
      XivChatType.Ls7,
      XivChatType.Ls8,
    ];

    ScrollingNode<VerticalListNode> chatChannelVoicesContainerNode = new()
    {
      ContentNode = {
        FitContents = true,
      },
      Size = new Vector2(320.0f, 345.0f),
    };
    _chatChannelVoicesOverlayNode.AttachContent(chatChannelVoicesContainerNode);

    _chatChannelVoicesEnabledNode = new CheckboxNode()
    {
      String = "Enable Chat Channel Voices",
      Size = new Vector2(230.0f, 20.0f),
      OnClick = (value) =>
      {
        _chatChannelVoicesApplyNode.IsEnabled = true;
        foreach (KeyValuePair<XivChatType, (StringDropDownNode maleNode, StringDropDownNode femaleNode)> kvp in _chatChannelVoicesNodes)
        {
          kvp.Value.maleNode.IsEnabled = value;
          kvp.Value.maleNode.Alpha = value ? 1.0f : 0.5f;
          kvp.Value.femaleNode.IsEnabled = value;
          kvp.Value.femaleNode.Alpha = value ? 1.0f : 0.5f;
        }
      }
    };
    chatChannelVoicesContainerNode.ContentNode.AddNode(_chatChannelVoicesEnabledNode);
    chatChannelVoicesContainerNode.ContentNode.AddDummy(4.0f);

    foreach (XivChatType chatType in chatTypes)
    {
      chatChannelVoicesContainerNode.ContentNode.AddNode(new HorizontalLineNode
      {
        Size = new Vector2(310.0f, 4.0f),
      });

      chatChannelVoicesContainerNode.ContentNode.AddDummy(4.0f);
      chatChannelVoicesContainerNode.ContentNode.AddNode(new LabelTextNode
      {
        String = " " + chatType.ToString(),
        Height = 18.0f,
        FontSize = 14,
      });
      chatChannelVoicesContainerNode.ContentNode.AddDummy(2.0f);

      StringDropDownNode maleNode = new()
      {
        Size = new Vector2(155.0f, 24.0f),
      };

      StringDropDownNode femaleNode = new()
      {
        Size = new Vector2(155.0f, 24.0f),
      };

      chatChannelVoicesContainerNode.ContentNode.AddNode(new HorizontalListNode
      {
        Height = 24.0f,
        InitialNodes = [
          maleNode,
          femaleNode
        ]
      });

      chatChannelVoicesContainerNode.ContentNode.AddDummy(4.0f);

      _chatChannelVoicesNodes.Add(chatType, (maleNode, femaleNode));
    }

    chatChannelVoicesContainerNode.ContentNode.RecalculateLayout();
    chatChannelVoicesContainerNode.RecalculateSizes();

    _chatChannelVoicesOverlayNode.AttachContent(new HorizontalLineNode
    {
      Y = chatChannelVoicesContainerNode.Height,
      Size = new Vector2(320.0f, 4.0f),
    });

    _chatChannelVoicesApplyNode = new TextButtonNode
    {
      String = "Apply",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(75.0f, chatChannelVoicesContainerNode.Height + 8.0f),
      OnClick = () =>
      {
        _chatChannelVoicesOverlayNode.IsVisible = false;
        _chatChannelVoicesOnApply?.Invoke();
      }
    };
    _chatChannelVoicesOverlayNode.AttachContent(_chatChannelVoicesApplyNode);

    _chatChannelVoicesOverlayNode.AttachContent(new TextButtonNode
    {
      String = "Close",
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(195.0f, chatChannelVoicesContainerNode.Height + 8.0f),
      OnClick = () =>
      {
        _chatChannelVoicesOverlayNode.IsVisible = false;
        ConfigurationSaved(); // Update the dropdowns
      }
    });

    _allowedVoicesOverlayNode = new ConfigOverlayNode(_services);
    _allowedVoicesOverlayNode.AttachNode(this);
    _allowedVoicesOverlayNode.Title = "Allowed Voices";

    _allowedVoicesContainerNode = new()
    {
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
        Icon = CircleButtonIcon.RightArrow,
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
        Icon = CircleButtonIcon.RightArrow,
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

    _localTTSService.OnInitialized += ConfigurationSaved;
  }

  protected override void Dispose(bool disposing, bool isNativeDestructor)
  {
    _localTTSService.OnInitialized -= ConfigurationSaved;
    base.Dispose(disposing, isNativeDestructor);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _tabBarNode.Size = new Vector2(Width, 24.0f);
    _overrideOverlayNode.SetSize(Size);
    _chatChannelVoicesOverlayNode.SetSize(Size);
    _allowedVoicesOverlayNode.SetSize(Size);
    NativeUtils.FixSliderNode(_overrideOverlayPitchNode);
    _localTTSOverridesListNode.Size = new Vector2(Width, Height - _localTTSOverridesSectionNode.Y - _localTTSOverridesListNode.Y);
  }

  public override void ConfigurationSaved()
  {
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

    List<string> maleAndFemaleVoices = maleVoices.Concat(["=== FEMALE ==="]).Concat(femaleVoices).ToList();
    List<string> femaleAndMaleVoices = femaleVoices.Concat(["=== MALE ==="]).Concat(maleVoices).ToList();

    _gameInteropService.RunOnFrameworkThread(() =>
    {
      _localTTSDefaultVoiceNode.SelectedOption = _configuration.LocalTTSDefaultVoice;

      _localTTSMaleVoiceNode.Options = maleAndFemaleVoices;
      _localTTSMaleVoiceNode.SelectedOption = _configuration.LocalTTSMaleVoice;

      _localTTSFemaleVoiceNode.Options = femaleAndMaleVoices;
      _localTTSFemaleVoiceNode.SelectedOption = _configuration.LocalTTSFemaleVoice;

      _localTTSCPUUsageNode.SelectedOption = _configuration.LocalTTSThreads switch
      {
        2 => "Low",
        4 => "Medium",
        8 => "High",
        _ => "Unknown"
      };

      _localTTSVoiceRandomizationNode.IsChecked = _configuration.LocalTTSVoiceRandomization;
      _localTTSVoiceRandomizationNode.Label.TextColor = (_configuration.LocalTTSChatChannelVoicesEnabled && _localTTSVoiceRandomizationNode.IsChecked) ? ColorHelper.GetColor(25) : ColorHelper.GetColor(8);
      _localTTSVoiceRandomizationNode.Label.TextFlags = (_configuration.LocalTTSChatChannelVoicesEnabled && _localTTSVoiceRandomizationNode.IsChecked) ? _localTTSVoiceRandomizationNode.Label.TextFlags | TextFlags.Italic : _localTTSVoiceRandomizationNode.Label.TextFlags & ~TextFlags.Italic;
      _localTTSVoiceRandomizationNode.TextTooltip = (_configuration.LocalTTSChatChannelVoicesEnabled && _localTTSVoiceRandomizationNode.IsChecked) ? "You have Chat Channel Voices enabled, this will disable voice randomization for chat messages." : string.Empty;

      _localTTSPitchRandomizationNode.IsChecked = _configuration.LocalTTSPitchRandomization;

      _localTTSOverridesListNode.Options = _configuration.LocalTTSOverrides.Select(kv => (kv.Key, kv.Value)).ToList();

      _overrideOverlayVoiceNode.Options = maleVoices.Concat(femaleVoices).ToList();

      _chatChannelVoicesApplyNode.IsEnabled = false;
      _chatChannelVoicesEnabledNode.IsChecked = _configuration.LocalTTSChatChannelVoicesEnabled;
      foreach (KeyValuePair<XivChatType, (StringDropDownNode maleNode, StringDropDownNode femaleNode)> kvp in _chatChannelVoicesNodes)
      {
        bool success = _configuration.LocalTTSChatChannelVoices.TryGetValue(kvp.Key, out (string? male, string? female) chatChannelVoices);
        kvp.Value.maleNode.Options = (new[] { "Default Male" }).Concat(maleAndFemaleVoices).ToList();
        kvp.Value.maleNode.SelectedOption = success ? chatChannelVoices.male ?? "Default Male" : "Default Male";
        kvp.Value.maleNode.OnOptionSelected = (_) => _chatChannelVoicesApplyNode.IsEnabled = true;
        kvp.Value.maleNode.IsEnabled = _configuration.LocalTTSChatChannelVoicesEnabled;
        kvp.Value.maleNode.Alpha = _configuration.LocalTTSChatChannelVoicesEnabled ? 1.0f : 0.5f;
        kvp.Value.femaleNode.Options = (new[] { "Default Female" }).Concat(femaleAndMaleVoices).ToList();
        kvp.Value.femaleNode.SelectedOption = success ? chatChannelVoices.female ?? "Default Female" : "Default Female";
        kvp.Value.femaleNode.OnOptionSelected = (_) => _chatChannelVoicesApplyNode.IsEnabled = true;
        kvp.Value.femaleNode.IsEnabled = _configuration.LocalTTSChatChannelVoicesEnabled;
        kvp.Value.femaleNode.Alpha = _configuration.LocalTTSChatChannelVoicesEnabled ? 1.0f : 0.5f;
      }

      _allowedVoicesApplyNode.IsEnabled = false;
      _allowedVoicesContainerNode.ContentNode.Height = 0.0f;
      _allowedVoicesMaleLabelNode.Y = _allowedVoicesContainerNode.ContentNode.Height;
      _allowedVoicesMaleLabelNode.X = 8.0f;
      _allowedVoicesContainerNode.ContentNode.Height = _allowedVoicesMaleLabelNode.Y + _allowedVoicesMaleLabelNode.Height + 5.0f;
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
        checkboxNode.Y = _allowedVoicesContainerNode.ContentNode.Height;
        checkboxNode.X = 8.0f;
        previewNode.Y = _allowedVoicesContainerNode.ContentNode.Height - 2.0f;
        previewNode.X = 100.0f;
        previewNode.OnClick = () => _messageDispatcher.DispatchLocalTTSMessage(voice, 100, $"This is a preview of {voice}.");
        _allowedVoicesContainerNode.ContentNode.Height = checkboxNode.Y + checkboxNode.Height + 5.0f;
      }

      float prevContentHeight = _allowedVoicesContainerNode.ContentNode.Height;
      _allowedVoicesContainerNode.ContentNode.Height = 0.0f;
      _allowedVoicesFemaleLabelNode.Y = _allowedVoicesContainerNode.ContentNode.Height;
      _allowedVoicesFemaleLabelNode.X = 150.0f;
      _allowedVoicesContainerNode.ContentNode.Height = _allowedVoicesFemaleLabelNode.Y + _allowedVoicesFemaleLabelNode.Height + 5.0f;
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
        checkboxNode.Y = _allowedVoicesContainerNode.ContentNode.Height;
        checkboxNode.X = 150.0f;
        previewNode.Y = _allowedVoicesContainerNode.ContentNode.Height - 2.0f;
        previewNode.X = 250.0f;
        previewNode.OnClick = () => _messageDispatcher.DispatchLocalTTSMessage(voice, 100, $"This is a preview of {voice}.");
        _allowedVoicesContainerNode.ContentNode.Height = checkboxNode.Y + checkboxNode.Height + 5.0f;
      }

      if (prevContentHeight > _allowedVoicesContainerNode.ContentNode.Height)
        _allowedVoicesContainerNode.ContentNode.Height = prevContentHeight;

      _allowedVoicesContainerNode.RecalculateSizes();
    });
  }

  public override void OnUpdate()
  {
    _localTTSOverridesListNode.OnUpdate();
  }
}
