using System.Runtime.Serialization;
using Dalamud.Configuration;

namespace XivVoices;

[Serializable]
public enum UnfocusedBehavior
{
  Play,
  Pause,
  Mute,
}

[Serializable]
public enum PlaybackDeviceType
{
  WaveOut,
  DirectSound,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
  public int Version { get; set; } = 0;
  public string? DataDirectory = null;

  // Dialogue Settings
  public bool ChatEnabled = true;
  public bool ChatSayEnabled = true;
  public bool ChatTellEnabled = true;
  public bool ChatPartyEnabled = true;
  public bool ChatShoutYellEnabled = true;
  public bool ChatAllianceEnabled = true;
  public bool ChatFreeCompanyEnabled = true;
  public bool ChatLinkshellEnabled = true;
  public bool ChatEmotesEnabled = true;
  public bool QueueChatMessages = true;
  public bool LocalTTSPlayerSays = false;
  public bool LocalTTSIgnoreChatDuringCutscenes = false;
  public bool LocalTTSDisableLocalPlayerChat = false;

  public bool QueueDialogue = true;

  public bool AddonTalkEnabled = true;
  public bool AddonBattleTalkEnabled = true;
  public bool AddonMiniTalkEnabled = true;

  public bool AddonTalkTTSEnabled = true;
  public bool AddonBattleTalkTTSEnabled = true;
  public bool AddonMiniTalkTTSEnabled = true;

  public bool AddonTalkNarratorEnabled = true;
  public bool AddonBattleTalkNarratorEnabled = true;

  public bool LipSyncEnabled = true;
  public bool AutoAdvanceEnabled = true;
  public bool FastForward = false;
  public bool AlwaysFastForward = false;
  public bool RetainersEnabled = true;
  public bool VoicePlayerChoices = true;
  public bool ReplaceVoicedARRCutscenes = false;
  public bool PreventAccidentalDialogueAdvance = false;

  // Playback Settings
  public bool MuteEnabled = false;
  public bool RespectFFXIVMasterVolumeToggle = true;

  public PlaybackDeviceType PlaybackDeviceType = PlaybackDeviceType.WaveOut;
  public string? WaveOutDevice = null;
  public Guid? DirectSoundDevice = null;

  public int Speed = 100;
  public int Volume = 100;

  public UnfocusedBehavior UnfocusedBehavior = UnfocusedBehavior.Play;

  public bool DirectionalAudioForChat = false;
  public bool DirectionalAudioForAddonMiniTalk = true;
  public int MaximumPan = 95;

  // LocalTTS Settings
  public int LocalTTSVolume = 100;
  public int LocalTTSSpeed = 100;

  public string LocalTTSDefaultVoice = "Male";
  public string LocalTTSMaleVoice = "Echo";
  public string LocalTTSFemaleVoice = "Heart";
  public int LocalTTSThreads = 2;

  public bool LocalTTSVoiceRandomization = true;
  public bool LocalTTSPitchRandomization = true;
  public List<string> LocalTTSDisallowedVoices = [];

  public bool LocalTTSForced = false;
  public bool LocalTTSRemoteEnabled = false;
  public bool LocalTTSRemoteFPSLimit = false;
  public string LocalTTSRemoteUri = "http://127.0.0.1:6969/tts?npc=%n&voice=%v&speaker=%s&sentence=%t";

  public Dictionary<string, (string voice, int pitch)> LocalTTSOverrides = [];
  public Dictionary<string, string> LocalTTSLexicon = [];

  // Overlay Settings
  public bool OverlayOpen = true;
  public int OverlayScale = 100;
  public Vector2 OverlayPosition = new(100.0f);
  public bool OverlayPinned = true;
  public bool OverlayBorder = true;
  public bool OverlayExpanded = true;
  public bool OverlayHideInDuty = false;
  public bool OverlayHideInCombat = false;
  public bool OverlayHideWhenMuted = false;

  // Audio History
  public bool EnableAutomaticReports = true;
  public bool LogReportsToChat = false;

  // Debug Settings
  public bool DebugMode = false;
  public bool DebugLogging = true;
  public string? ServerUrl = null;
  public bool LiveMode = false;
  public bool WarnIgnoredSpeaker = false;
  public XivChatType DefaultChatChannel = XivChatType.Debug;

  [NonSerialized]
  private ILogger? Logger;
  [NonSerialized]
  private IDalamudPluginInterface? PluginInterface;
  [NonSerialized]
  private Dictionary<string, object?>? _previousConfig;

  public void Initialize(ILogger logger, IDalamudPluginInterface pluginInterface)
  {
    Logger = logger;
    PluginInterface = pluginInterface;

    Logger.SetConfiguration(this);
    ConfigurationMigrator.Migrate(this, Logger!);

    // Can remove this eventually, i guess.
    LocalTTSDisallowedVoices = LocalTTSDisallowedVoices.Distinct().ToList();
    Save();

    _previousConfig = GetAllFields();
    Logger.Debug(JsonSerializer.Serialize(_previousConfig, JsonOptions.Write));
  }

  private bool IsKeySecret(string key)
  {
    if (key == "SecretKey") return true;
    return false;
  }

  [OnDeserialized]
  internal void OnDeserializedMethod(StreamingContext context)
  {
    // Defaults for arrays, newtonsoft seems to add to them instead of overwriting them,
    // and I don't know why or how to not make it do that. So here is a stupid workaround instead.
    // We add a dummy entry in Save() to these, so the Count is only 0 if it wasn't defined.
    if (LocalTTSDisallowedVoices.Count == 0) LocalTTSDisallowedVoices = ["Nicole"];
  }

  public event System.Action? Saved;

  public void Save()
  {
    if (!LocalTTSDisallowedVoices.Contains("__empty__")) LocalTTSDisallowedVoices.Add("__empty__");

    Dictionary<string, object?> currentConfig = GetAllFields();
    foreach (KeyValuePair<string, object?> field in GetAllFields())
    {
      if (_previousConfig != null &&
          _previousConfig.TryGetValue(field.Key, out object? oldValue) &&
          !Equals(oldValue, field.Value) && !IsKeySecret(field.Key))
      {
        Logger?.Debug($"Changed {field.Key}: from '{oldValue}' to '{field.Value}'");
      }
    }

    PluginInterface!.SavePluginConfig(this);
    _previousConfig = currentConfig;

    Saved?.Invoke();
  }

  private Dictionary<string, object?> GetAllFields() =>
    GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(x => x.Name, x => x.GetValue(this));

  public string SerializeToBase64(object obj)
  {
    string json = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
    byte[] bytes = Encoding.UTF8.GetBytes(json);
    using MemoryStream compressedStream = new();
    using (GZipStream zipStream = new(compressedStream, CompressionMode.Compress))
      zipStream.Write(bytes, 0, bytes.Length);
    return Convert.ToBase64String(compressedStream.ToArray());
  }

  public T? DeserializeFromBase64<T>(string base64)
  {
    try
    {
      byte[] bytes = Convert.FromBase64String(base64);
      using MemoryStream compressedStream = new(bytes);
      using GZipStream zipStream = new(compressedStream, CompressionMode.Decompress);
      using MemoryStream resultStream = new();
      zipStream.CopyTo(resultStream);
      bytes = resultStream.ToArray();
      string json = Encoding.UTF8.GetString(bytes);
      T? deserializedObject = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
      if (deserializedObject is T typedObject)
      {
        return typedObject;
      }
    }
    catch { }

    return default;
  }
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
