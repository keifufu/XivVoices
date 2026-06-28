namespace XivVoices.Windows;

using LocalTTSOverride = (string speaker, (string voice, int pitch) options);

public class LocalTTSOverrideItemNode : IconListItemNode<LocalTTSOverride>
{
  protected override uint GetIconId(LocalTTSOverride data) => data.speaker.Contains('@') ? 61515u : 61583u;
  protected override string GetLabelText(LocalTTSOverride data) => data.speaker;
  protected override string GetSubLabelText(LocalTTSOverride data) => $"Voice: {data.options.voice} - Pitch: {data.options.pitch}%";
}
