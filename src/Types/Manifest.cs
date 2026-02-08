namespace XivVoices.Types;

public class VoiceEntry
{
  public required string Id { get; set; }
  public required bool IsGeneric { get; set; }

  public override string ToString() =>
    JsonSerializer.Serialize(this, JsonOptions.Write);
}
public class NpcEntry
{
  // Id and VoiceId are nullable because GameInteropService can set them to be null for new npcs.
  public required string? Id { get; set; }
  public required string? VoiceId { get; set; }
  public required string Gender { get; set; }
  public required string Race { get; set; }
  public required string Tribe { get; set; }
  public required string Body { get; set; }
  public required string Eyes { get; set; }
  public required uint BaseId { get; set; }
  public required List<string> Speakers { get; set; }
  public required bool HasVariedLooks { get; set; }

  public override string ToString() =>
    JsonSerializer.Serialize(this, JsonOptions.Write);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpeakerMappingType
{
  Retainer,
  Nameless
}

public class SpeakerMappingEntry
{
  public required SpeakerMappingType Type { get; set; }
  public required string? Speaker { get; set; }
  public required string Sentence { get; set; }
  public required string? NpcId { get; set; }
}

public class LexiconEntry
{
  public required string From { get; set; }
  public required string To { get; set; }
}

public class ManifestJson
{
  public required string ToolsMd5 { get; set; }
  public required List<VoiceEntry> Voices { get; set; }
  public required List<NpcEntry> Npcs { get; set; }
  public required Dictionary<string, long> Voicelines { get; set; }
  public required List<string> IgnoredSpeakers { get; set; }
  public required List<SpeakerMappingEntry> SpeakerMappings { get; set; }
  public required List<LexiconEntry> Lexicon { get; set; }
}

public class Manifest
{
  // Unchanged
  public required string ToolsMd5 { get; set; }

  // Id -> VoiceEntry
  public required Dictionary<string, VoiceEntry> Voices { get; set; }

  // Each entry in NpcEntry.Speakers -> NpcEntry
  public required Dictionary<string, NpcEntry> Npcs { get; set; }

  // Pre-calculated mappings to find generic voices.
  public required Dictionary<string, NpcEntry> Npcs_Generic { get; set; }

  // Unchanged
  public required Dictionary<string, long> Voicelines { get; set; }

  // Unchanged
  public required List<string> IgnoredSpeakers { get; set; }

  // Type -> ((Speaker, Sentence) -> NpcId)
  public required Dictionary<SpeakerMappingType, Dictionary<(string? speaker, string sentence), string?>> SpeakerMappings { get; set; }

  // From -> To
  public required Dictionary<string, string> Lexicon { get; set; }
}
