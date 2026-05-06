using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class DialogueSettingsTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.DialogueSettings;
  private Configuration _configuration = null!;

  private CheckboxNode _chatEnabledNode = null!;
  private CheckboxNode _chatSayEnabledNode = null!;
  private CheckboxNode _chatTellEnabledNode = null!;
  private CheckboxNode _chatPartyEnabledNode = null!;
  private CheckboxNode _chatShoutYellEnabledNode = null!;
  private CheckboxNode _chatAllianceEnabledNode = null!;
  private CheckboxNode _chatFreeCompanyEnabledNode = null!;
  private CheckboxNode _chatLinkshellEnabledNode = null!;
  private CheckboxNode _chatEmotesEnabledNode = null!;
  private CheckboxNode _queueChatMessagesNode = null!;
  private CheckboxNode _localTTSPlayerSaysNode = null!;
  private CheckboxNode _localTTSDisableLocalPlayerChatNode = null!;

  private CheckboxNode _queueDialogueNode = null!;

  private CheckboxNode _addonTalkEnabledNode = null!;
  private CheckboxNode _addonTalkTTSEnabledNode = null!;
  private CheckboxNode _addonTalkNarratorEnabledNode = null!;

  private CheckboxNode _addonBattleTalkEnabledNode = null!;
  private CheckboxNode _addonBattleTalkTTSEnabledNode = null!;
  private CheckboxNode _addonBattleTalkNarratorEnabledNode = null!;

  private CheckboxNode _addonMiniTalkEnabledNode = null!;
  private CheckboxNode _addonMiniTalkTTSEnabledNode = null!;

  private CheckboxNode _lipSyncNode = null!;
  private CheckboxNode _autoAdvanceEnabledNode = null!;
  private CheckboxNode _fastForwardNode = null!;
  private CheckboxNode _retainersEnabledNode = null!;
  private CheckboxNode _replaceVoicedARRCutscenesNode = null!;
  private CheckboxNode _preventAccidentalDialogueAdvanceNode = null!;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();

    ConfigSectionNode chatSettingsSectionNode = new("Chat Settings");

    chatSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = "Chat Messages are voiced using LocalTTS.",
    }, inline: true);

    _chatEnabledNode = new()
    {
      String = "Chat Messages Enabled",
      Size = new Vector2(190.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.ChatEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatEnabledNode);

    _chatSayEnabledNode = new()
    {
      String = "Say",
      Size = new Vector2(110.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.ChatSayEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatSayEnabledNode);

    _chatAllianceEnabledNode = new()
    {
      String = "Alliance",
      Size = new Vector2(130.0f, 20.0f),
      X = 200.0f,
      OnClick = (value) =>
      {
        _configuration.ChatAllianceEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatAllianceEnabledNode, inline: true);

    _chatTellEnabledNode = new()
    {
      String = "Tell",
      Size = new Vector2(110.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.ChatTellEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatTellEnabledNode);

    _chatFreeCompanyEnabledNode = new()
    {
      String = "Free Company",
      Size = new Vector2(130.0f, 20.0f),
      X = 200.0f,
      OnClick = (value) =>
      {
        _configuration.ChatFreeCompanyEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatFreeCompanyEnabledNode, inline: true);

    _chatPartyEnabledNode = new()
    {
      String = "Party",
      Size = new Vector2(110.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.ChatPartyEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatPartyEnabledNode);

    _chatLinkshellEnabledNode = new()
    {
      String = "Linkshell",
      Size = new Vector2(130.0f, 20.0f),
      X = 200.0f,
      OnClick = (value) =>
      {
        _configuration.ChatLinkshellEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatLinkshellEnabledNode, inline: true);

    _chatShoutYellEnabledNode = new()
    {
      String = "Shout / Yell",
      Size = new Vector2(110.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.ChatShoutYellEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatShoutYellEnabledNode);

    _chatEmotesEnabledNode = new()
    {
      String = "Emotes",
      Size = new Vector2(130.0f, 20.0f),
      X = 200.0f,
      OnClick = (value) =>
      {
        _configuration.ChatEmotesEnabled = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_chatEmotesEnabledNode, inline: true);

    _queueChatMessagesNode = new()
    {
      String = "Queue Chat Messages",
      Size = new Vector2(190.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.QueueChatMessages = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_queueChatMessagesNode);

    _localTTSPlayerSaysNode = new()
    {
      String = "Add \"<Player> says\" to Chat Messanges",
      Size = new Vector2(310.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSPlayerSays = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_localTTSPlayerSaysNode);

    _localTTSDisableLocalPlayerChatNode = new()
    {
      String = "Ignore your own Chat Messages",
      Size = new Vector2(260.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LocalTTSDisableLocalPlayerChat = value;
        _configuration.Save();
      }
    };
    chatSettingsSectionNode.AttachNode(_localTTSDisableLocalPlayerChatNode);

    AttachNode(chatSettingsSectionNode);

    ConfigSectionNode dialogueSettingsSectionNode = new("Dialogue Settings", chatSettingsSectionNode);

    dialogueSettingsSectionNode.AttachNode(new ConfigTooltipNode()
    {
      TextTooltip = """
      Select which type of dialogue will be voiced.

      Enabled
      Master toggle, will use proper voicelines when available.

      TTS
      When proper voicelines are not available, will fall back to Local TTS.

      Narrator
      Whether lines without a speaker (e.g. System Messages, Books) should be voiced.
      These can also fall back to Local TTS.
      """,
    }, inline: true);

    _queueDialogueNode = new()
    {
      String = "Queue Dialogue",
      TextTooltip = "Queues regular dialogue so it won't get skipped when you click away.",
      Size = new Vector2(150.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.QueueDialogue = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_queueDialogueNode);

    dialogueSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Regular Dialogue",
      Height = 16.0f,
    }, padding: 4.0f);

    _addonTalkEnabledNode = new()
    {
      String = "Enabled",
      Size = new Vector2(90.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.AddonTalkEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonTalkEnabledNode, padding: 2.0f);

    _addonTalkTTSEnabledNode = new()
    {
      String = "TTS",
      Size = new Vector2(60.0f, 20.0f),
      X = 145.0f,
      OnClick = (value) =>
      {
        _configuration.AddonTalkTTSEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonTalkTTSEnabledNode, inline: true);

    _addonTalkNarratorEnabledNode = new()
    {
      String = "Narrator",
      Size = new Vector2(90.0f, 20.0f),
      X = 220.0f,
      OnClick = (value) =>
      {
        _configuration.AddonTalkNarratorEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonTalkNarratorEnabledNode, inline: true);

    dialogueSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Battle Dialogue",
      Height = 16.0f,
    }, padding: 4.0f);

    _addonBattleTalkEnabledNode = new()
    {
      String = "Enabled",
      Size = new Vector2(90.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.AddonBattleTalkEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonBattleTalkEnabledNode, padding: 2.0f);

    _addonBattleTalkTTSEnabledNode = new()
    {
      String = "TTS",
      Size = new Vector2(60.0f, 20.0f),
      X = 145.0f,
      OnClick = (value) =>
      {
        _configuration.AddonBattleTalkTTSEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonBattleTalkTTSEnabledNode, inline: true);

    _addonBattleTalkNarratorEnabledNode = new()
    {
      String = "Narrator",
      Size = new Vector2(90.0f, 20.0f),
      X = 220.0f,
      OnClick = (value) =>
      {
        _configuration.AddonBattleTalkNarratorEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonBattleTalkNarratorEnabledNode, inline: true);

    dialogueSettingsSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Bubble Dialogue",
      Height = 16.0f,
    }, padding: 4.0f);

    _addonMiniTalkEnabledNode = new()
    {
      String = "Enabled",
      Size = new Vector2(90.0f, 20.0f),
      X = ConfigSectionNode.Indent,
      OnClick = (value) =>
      {
        _configuration.AddonMiniTalkEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonMiniTalkEnabledNode, padding: 2.0f);

    _addonMiniTalkTTSEnabledNode = new()
    {
      String = "TTS",
      Size = new Vector2(60.0f, 20.0f),
      X = 145.0f,
      OnClick = (value) =>
      {
        _configuration.AddonMiniTalkTTSEnabled = value;
        _configuration.Save();
      }
    };
    dialogueSettingsSectionNode.AttachNode(_addonMiniTalkTTSEnabledNode, inline: true);

    AttachNode(dialogueSettingsSectionNode);

    ConfigSectionNode otherSettingsNode = new("Other Settings", dialogueSettingsSectionNode);

    _lipSyncNode = new()
    {
      String = "LipSync",
      TextTooltip = """
      Will attempt to lipsync characters while they're speaking.
      Might not work in certain cutscenes.
      """,
      Size = new Vector2(90.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.LipSyncEnabled = value;
        _configuration.Save();
      }
    };
    otherSettingsNode.AttachNode(_lipSyncNode);

    _autoAdvanceEnabledNode = new()
    {
      String = "Auto-Advance",
      TextTooltip = """
      Automatically advances to the next dialogue when audio finishes playing.
      Hold ALT on a keyboard, or Y / Triangle on a controller, to temporarily pause it.
      The spinner in the bottom-right of the dialogue box shows the auto-advance status.
      """,
      Size = new Vector2(130.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.AutoAdvanceEnabled = value;
        _configuration.Save();
      }
    };
    otherSettingsNode.AttachNode(_autoAdvanceEnabledNode);

    _fastForwardNode = new()
    {
      String = "Fast-Forward",
      TextTooltip = "Dialogue boxes will be skipped immediately, useful if used in combination with \"Queue Dialogue\". Will not take effect if muted.",
      Size = new Vector2(120.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.FastForward = value;
        _configuration.Save();
      }
    };
    otherSettingsNode.AttachNode(_fastForwardNode);

    _retainersEnabledNode = new()
    {
      String = "Retainers Enabled",
      Size = new Vector2(160.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.RetainersEnabled = value;
        _configuration.Save();
      }
    };
    otherSettingsNode.AttachNode(_retainersEnabledNode);

    _replaceVoicedARRCutscenesNode = new()
    {
      String = "Replace Voiced ARR Cutscenes",
      Size = new Vector2(250.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.ReplaceVoicedARRCutscenes = value;
        _configuration.Save();
      }
    };
    otherSettingsNode.AttachNode(_replaceVoicedARRCutscenesNode);

    _preventAccidentalDialogueAdvanceNode = new()
    {
      String = "Prevent Accidental Dialogue Advance",
      TextTooltip = "Prevents advancing dialogue when left-clicking unless hovering over the actual dialogue box. (Disabled while plugin is muted)",
      Size = new Vector2(300.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.PreventAccidentalDialogueAdvance = value;
        _configuration.Save();
      }
    };
    otherSettingsNode.AttachNode(_preventAccidentalDialogueAdvanceNode);

    AttachNode(otherSettingsNode);
  }

  public override void ConfigurationSaved()
  {
    _chatEnabledNode.IsChecked = _configuration.ChatEnabled;
    _chatSayEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatSayEnabledNode.IsChecked = _configuration.ChatSayEnabled;

    _chatTellEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatTellEnabledNode.IsChecked = _configuration.ChatTellEnabled;

    _chatPartyEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatPartyEnabledNode.IsChecked = _configuration.ChatPartyEnabled;

    _chatShoutYellEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatShoutYellEnabledNode.IsChecked = _configuration.ChatShoutYellEnabled;

    _chatAllianceEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatAllianceEnabledNode.IsChecked = _configuration.ChatAllianceEnabled;

    _chatFreeCompanyEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatFreeCompanyEnabledNode.IsChecked = _configuration.ChatFreeCompanyEnabled;

    _chatLinkshellEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatLinkshellEnabledNode.IsChecked = _configuration.ChatLinkshellEnabled;

    _chatEmotesEnabledNode.IsEnabled = _configuration.ChatEnabled;
    _chatEmotesEnabledNode.IsChecked = _configuration.ChatEmotesEnabled;

    _queueChatMessagesNode.IsEnabled = _configuration.ChatEnabled;
    _queueChatMessagesNode.IsChecked = _configuration.QueueChatMessages;

    _localTTSPlayerSaysNode.IsChecked = _configuration.LocalTTSPlayerSays;
    _localTTSDisableLocalPlayerChatNode.IsChecked = _configuration.LocalTTSDisableLocalPlayerChat;

    _queueDialogueNode.IsChecked = _configuration.QueueDialogue;

    _addonTalkEnabledNode.IsChecked = _configuration.AddonTalkEnabled;

    _addonTalkTTSEnabledNode.IsChecked = _configuration.AddonTalkTTSEnabled;
    _addonTalkTTSEnabledNode.IsEnabled = _configuration.AddonTalkEnabled;

    _addonTalkNarratorEnabledNode.IsChecked = _configuration.AddonTalkNarratorEnabled;
    _addonTalkNarratorEnabledNode.IsEnabled = _configuration.AddonTalkEnabled;

    _addonBattleTalkEnabledNode.IsChecked = _configuration.AddonBattleTalkEnabled;

    _addonBattleTalkTTSEnabledNode.IsChecked = _configuration.AddonBattleTalkTTSEnabled;
    _addonBattleTalkTTSEnabledNode.IsEnabled = _configuration.AddonBattleTalkEnabled;

    _addonBattleTalkNarratorEnabledNode.IsChecked = _configuration.AddonBattleTalkNarratorEnabled;
    _addonBattleTalkNarratorEnabledNode.IsEnabled = _configuration.AddonBattleTalkEnabled;

    _addonMiniTalkEnabledNode.IsChecked = _configuration.AddonMiniTalkEnabled;

    _addonMiniTalkTTSEnabledNode.IsChecked = _configuration.AddonMiniTalkTTSEnabled;
    _addonMiniTalkTTSEnabledNode.IsEnabled = _configuration.AddonMiniTalkEnabled;

    _lipSyncNode.IsChecked = _configuration.LipSyncEnabled;
    _autoAdvanceEnabledNode.IsChecked = _configuration.AutoAdvanceEnabled;
    _fastForwardNode.IsChecked = _configuration.FastForward;
    _retainersEnabledNode.IsChecked = _configuration.RetainersEnabled;
    _replaceVoicedARRCutscenesNode.IsChecked = _configuration.ReplaceVoicedARRCutscenes;
    _preventAccidentalDialogueAdvanceNode.IsChecked = _configuration.PreventAccidentalDialogueAdvance;
  }
}
