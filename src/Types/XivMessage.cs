namespace XivVoices.Types;

public class XivMessage(string id, MessageSource source, string voice, string speaker, string sentence, string originalSpeaker, string originalSentence, NpcData? npcData, string? voicelinePath)
{
  [JsonIgnore]
  public string Id { get; } = id;
  // Used as a unique key for LipSync and PlaybackService.Playing

  [JsonIgnore]
  public bool Reported { get; set; } = false;

  [JsonIgnore]
  public bool IsLocalTTS => VoicelinePath == null;

  public MessageSource Source { get; } = source;
  public string Voice { get; } = voice;
  public string Speaker { get; } = speaker;
  public string Sentence { get; } = sentence;
  public string OriginalSpeaker { get; } = originalSpeaker;
  public string OriginalSentence { get; } = originalSentence;
  public NpcData? NpcData { get; } = npcData;
  public string? VoicelinePath { get; } = voicelinePath;

  public override string ToString() =>
    $"{{ Voice:'{Voice}' Source:'{Source}' Speaker:'{Speaker}' Sentence:'{Sentence}' OriginalSpeaker:'{OriginalSpeaker}' OriginalSentence:'{OriginalSentence}' NpcData:'{NpcData}' VoicelinePath:'{VoicelinePath}' }}";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageSource
{
  AddonTalk,
  AddonMiniTalk,
  AddonBattleTalk,
  ChatMessage,
}
