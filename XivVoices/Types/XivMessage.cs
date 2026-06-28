namespace XivVoices.Types;

public class XivMessage
{
  public string Id { get; }
  public MessageSource Source { get; }

  public string Speaker { get; }
  public string Sentence { get; }

  public string RawSpeaker { get; }
  public string RawSentence { get; }

  public NpcEntry? Npc { get; }

  [JsonIgnore]
  public string? PlayerName { get; }

  public string AddName(string str)
  {
    if (PlayerName == null) return str;
    string[] fullname = PlayerName.Split(" ");
    return str.Replace("_FIRSTNAME_", fullname[0]).Replace("_LASTNAME_", fullname[1]);
  }

  [JsonIgnore]
  public VoiceEntry? Voice { get; }

  [JsonIgnore]
  public string? VoicelinePath;

  [JsonIgnore]
  public bool IsLocalTTS => VoicelinePath == null;

  [JsonIgnore]
  public bool IsGenerating = false;

  [JsonIgnore]
  public CancellationTokenSource GenerationToken = new();

  [JsonIgnore]
  public bool Reported { get; set; } = false;

  [JsonIgnore]
  public bool IsFake { get; set; } = false;

  [JsonIgnore]
  public string? VoiceOverride { get; set; } = null;

  [JsonIgnore]
  public int? PitchOverride { get; set; } = null;

  [JsonIgnore]
  public string? SpeakerWorld { get; set; } = null;

  [JsonIgnore]
  public XivChatType? ChatChannel { get; set; } = null;

  [JsonIgnore]
  public string? LocalTTSVoice { get; set; } = null;

  [JsonIgnore]
  public WaveStream? WaveStream { get; set; } = null;

  [JsonIgnore]
  public int RelativeVolume { get; set; } = 0;

  [JsonIgnore]
  public bool Queued { get; set; } = false;

  [JsonIgnore]
  public bool Replay { get; set; } = false;

  public override string ToString() =>
    JsonSerializer.Serialize(this, JsonOptions.Write);

  public XivMessage(
    string id,
    MessageSource source,
    string speaker,
    string sentence,
    string rawSpeaker,
    string rawSentence,
    NpcEntry? npc,
    VoiceEntry? voice,
    string? voicelinePath,
    string? playerName,
    bool isFake,
    string? voiceOverride,
    int? pitchOverride,
    string? speakerWorld,
    XivChatType? chatChannel)
  {
    Id = id;
    Source = source;
    Speaker = speaker;
    Sentence = sentence;
    RawSpeaker = rawSpeaker;
    RawSentence = rawSentence;
    Npc = npc;
    Voice = voice;
    VoicelinePath = voicelinePath;
    PlayerName = playerName;
    IsFake = isFake;
    VoiceOverride = voiceOverride;
    PitchOverride = pitchOverride;
    SpeakerWorld = speakerWorld;
    ChatChannel = chatChannel;
  }

  [JsonConstructor]
  public XivMessage(
    string id,
    MessageSource source,
    string speaker,
    string sentence,
    string rawSpeaker,
    string rawSentence,
    NpcEntry? npc)
  {
    Id = id;
    Source = source;
    Speaker = speaker;
    Sentence = sentence;
    RawSpeaker = rawSpeaker;
    RawSentence = rawSentence;
    Npc = npc;
  }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageSource
{
  AddonTalk,
  AddonMiniTalk,
  AddonBattleTalk,
  ChatMessage,
  SelectString
}
