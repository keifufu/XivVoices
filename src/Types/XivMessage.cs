namespace XivVoices.Types;

public class XivMessage(string id, MessageSource source, VoiceEntry? voice, string speaker, string sentence, string rawSpeaker, string rawSentence, NpcEntry? npc, string? voicelinePath, bool isQueued = false)
{
  public string Id { get; } = id;
  public MessageSource Source { get; } = source;

  public string Speaker { get; } = speaker;
  public string Sentence { get; } = sentence;

  public string RawSpeaker { get; } = rawSpeaker;
  public string RawSentence { get; } = rawSentence;

  public NpcEntry? Npc { get; } = npc;

  // Used for AudioPostProcessing right now,
  // we do some effects based on the voice name.
  // Can get this from the Manifest via Npc?.VoiceId,
  // but this is easier.
  [JsonIgnore]
  public VoiceEntry? Voice { get; } = voice;

  [JsonIgnore]
  public string? VoicelinePath => voicelinePath;

  [JsonIgnore]
  public bool IsLocalTTS => VoicelinePath == null;

  [JsonIgnore]
  public bool IsQueued = isQueued;

  [JsonIgnore]
  public bool IsGenerating = false;

  [JsonIgnore]
  public CancellationTokenSource GenerationToken = new();

  [JsonIgnore]
  public bool Reported { get; set; } = false;

  public override string ToString() =>
    JsonSerializer.Serialize(this);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageSource
{
  AddonTalk,
  AddonMiniTalk,
  AddonBattleTalk,
  ChatMessage,
}
