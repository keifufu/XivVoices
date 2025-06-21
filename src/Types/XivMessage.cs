namespace XivVoices.Types;

public class XivMessage(string id, MessageSource source, string voice, string speaker, string sentence, string originalSpeaker, string originalSentence, NpcData? npcData, string? voicelinePath)
{
  public string Id { get; } = id;
  public MessageSource Source { get; } = source;
  public string Voice { get; } = voice;
  public string Speaker { get; } = speaker;
  public string Sentence { get; } = sentence;

  // Used for Audio Logs UI and for printing system messages
  [JsonIgnore]
  public string OriginalSpeaker { get; } = originalSpeaker;
  [JsonIgnore]
  public string OriginalSentence { get; } = originalSentence;

  public NpcData? NpcData { get; } = npcData;

  [JsonIgnore]
  public string? VoicelinePath => voicelinePath;

  [JsonIgnore]
  public bool IsLocalTTS => VoicelinePath == null;

  [JsonIgnore]
  public bool Reported { get; set; } = false;

  public override string ToString() =>
    $"{{ Id:'{Id}' Source:'{Source}' Voice:'{Voice}' Speaker:'{Speaker}' Sentence:'{Sentence}' OriginalSpeaker:'{OriginalSpeaker}' OriginalSentence:'{OriginalSentence}' NpcData:'{NpcData}' VoicelinePath:'{VoicelinePath}' IsLocalTTS:'{IsLocalTTS}' Reported:'{Reported}' }}";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageSource
{
  AddonTalk,
  AddonMiniTalk,
  AddonBattleTalk,
  ChatMessage,
}
