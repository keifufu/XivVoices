namespace XivVoices.Types;

public class VoiceEntry
{
  public required string Name { get; set; }
  public required List<string> Speakers { get; set; }
}

public class NpcData
{
  public required string Gender { get; set; }
  public required string Race { get; set; }
  public required string Tribe { get; set; }
  public required string Body { get; set; }
  public required string Eyes { get; set; }
  public required string Type { get; set; }
  public required uint BaseId { get; set; }

  public override string ToString() =>
    $"{{ Gender:'{Gender}' Race:'{Race}' Tribe:'{Tribe}' Body:'{Body}' Eyes:'{Eyes}' Type:'{Type}' BaseId:'{BaseId}' }}";
}

public class ManifestJson
{
  public required string ToolsMd5 { get; set; }
  public required Dictionary<string, long> Voicelines { get; set; }
  public required List<string> IgnoredSpeakers { get; set; }
  public required List<VoiceEntry> Voices { get; set; }
  public required Dictionary<string, string> Nameless { get; set; }
  public required Dictionary<string, NpcData> NpcData { get; set; }
  public required Dictionary<string, string> Retainers { get; set; }
  public required Dictionary<string, string> Lexicon { get; set; }
  public required List<string> NpcsWithVariedLooks { get; set; }
  public required List<string> NpcsWithRetainerLines { get; set; }
}

public class Manifest
{
  public required string ToolsMd5 { get; set; }
  public required Dictionary<string, long> Voicelines { get; set; }
  public required List<string> IgnoredSpeakers { get; set; }
  public required Dictionary<string, string> Voices { get; set; }
  public required Dictionary<string, string> Nameless { get; set; }
  public required Dictionary<string, NpcData> NpcData { get; set; }
  public required Dictionary<string, string> Retainers { get; set; }
  public required Dictionary<string, string> Lexicon { get; set; }
  public required List<string> NpcsWithVariedLooks { get; set; }
  public required List<string> NpcsWithRetainerLines { get; set; }
}
