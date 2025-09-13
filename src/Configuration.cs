using Dalamud.Configuration;

namespace XivVoices;

[Serializable]
public class Configuration : IPluginConfiguration
{
  public int Version { get; set; } = 0;
  public string? DataDirectory = null;

  // Dialogue Settings
  public bool QueueChatMessages = true;
  public bool ChatSayEnabled = true;
  public bool ChatTellEnabled = true;
  public bool ChatShoutYellEnabled = true;
  public bool ChatPartyEnabled = true;
  public bool ChatAllianceEnabled = true;
  public bool ChatFreeCompanyEnabled = true;
  public bool ChatLinkshellEnabled = true;
  public bool ChatEmotesEnabled = true;

  public bool AddonTalkEnabled = true;
  public bool AddonBattleTalkEnabled = true;
  public bool AddonMiniTalkEnabled = true;

  public bool AddonTalkTTSEnabled = true;
  public bool AddonBattleTalkTTSEnabled = true;
  public bool AddonMiniTalkTTSEnabled = true;

  public bool AddonTalkNarratorEnabled = true;
  public bool AddonBattleTalkNarratorEnabled = true;

  public bool AutoAdvanceEnabled = true;
  public bool RetainersEnabled = true;
  public bool PrintBubbleMessages = true;
  public bool PrintNarratorMessages = true;
  public bool ReplaceVoicedARRCutscenes = true;

  // Audio Settings
  public bool MuteEnabled = false;
  public bool LipSyncEnabled = false;
  public bool QueueDialogue = false;

  public int Speed = 100;
  public int Volume = 100;

  public bool DirectionalAudioForChat = false;
  public bool DirectionalAudioForAddonMiniTalk = true;

  public bool LocalTTSEnabled = true;
  public string LocalTTSDefaultVoice = "Male";
  public int LocalTTSVolume = 100;
  public int LocalTTSSpeed = 100;

  public bool LocalTTSPlayerSays = true;

  // Audio Logs
  public bool EnableAutomaticReports = true;
  public bool LogReportsToChat = false;

  // Wine Settings
  public bool WineUseNativeFFmpeg = true;
  public string ProtonUsername = "";

  // Debug Settings
  public bool DebugMode = false;
  public bool DebugLogging = true;
  public string? ServerUrl = null;
  public string LocalTTSVoiceMale = "en-gb-northern_english_male-medium";
  public string LocalTTSVoiceFemale = "en-gb-jenny_dioco-medium";
  public bool EnableLocalGeneration = false;
  public bool ForceLocalGeneration = false;
  public string LocalGenerationUri = "http://127.0.0.1:6969/generate?voice=%v&sentence=%s&id=%i";

  [NonSerialized]
  private ILogger? Logger;
  [NonSerialized]
  private IDalamudPluginInterface? PluginInterface;

  public void Initialize(ILogger logger, IDalamudPluginInterface pluginInterface)
  {
    Logger = logger;
    PluginInterface = pluginInterface;

    Logger.SetConfiguration(this);
    ConfigurationMigrator.Migrate(this, Logger!);

    if (MuteEnabled)
      Logger.Chat("Plugin is muted. \"/xivv mute\" to unmute.");
  }

  public void Save() => PluginInterface!.SavePluginConfig(this);
}

public static class ConfigurationMigrator
{
  public static void Migrate(Configuration configuration, ILogger logger)
  {
    // if (configuration.Version == 0)
    // {
    //   // Migrate here
    //   configuration.Version = 1;
    //   configuration.Save();
    // }
    // else
    {
      logger.Debug($"Configuration up-to-date: v{configuration.Version}");
    }
  }
}
