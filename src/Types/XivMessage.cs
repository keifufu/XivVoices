namespace XivVoices.Types;

public class XivMessage(string id, MessageSource source, VoiceEntry? voice, string speaker, string sentence, string originalSpeaker, string originalSentence, NpcEntry? npc, string? voicelinePath)
{
  public string Id { get; } = id;
  public MessageSource Source { get; } = source;
  public VoiceEntry? Voice { get; } = voice;
  public string Speaker { get; } = speaker;
  public string Sentence { get; } = sentence;

  // Used for Audio Logs UI and for printing system messages
  [JsonIgnore]
  public string OriginalSpeaker { get; } = originalSpeaker;
  [JsonIgnore]
  public string OriginalSentence { get; } = originalSentence;

  public NpcEntry? Npc { get; } = npc;

  [JsonIgnore]
  public string? VoicelinePath => voicelinePath;

  [JsonIgnore]
  public bool IsLocalTTS => VoicelinePath == null;

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
