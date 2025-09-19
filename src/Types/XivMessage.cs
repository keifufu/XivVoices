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

  // Used for AudioPostProcessing right now,
  // we do some effects based on the voice name.
  // Can get this from the Manifest via Npc?.VoiceId,
  // but this is easier.
  [JsonIgnore]
  public VoiceEntry? Voice { get; }

  [JsonIgnore]
  public string? VoicelinePath;

  [JsonIgnore]
  public bool IsLocalTTS => VoicelinePath == null;

  [JsonIgnore]
  public bool IsQueued = false;

  [JsonIgnore]
  public bool IsGenerating = false;

  [JsonIgnore]
  public CancellationTokenSource GenerationToken = new();

  [JsonIgnore]
  public bool Reported { get; set; } = false;

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
    bool isQueued = false)
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
    IsQueued = isQueued;
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
}
