namespace XivVoices.Windows;

using LocalTTSLexicon = (string from, string to);

public class LocalTTSLexiconItemNode : IconListItemNode<LocalTTSLexicon>
{
  protected override uint GetIconId(LocalTTSLexicon data) => 61512;
  protected override string GetLabelText(LocalTTSLexicon data) => data.from;
  protected override string GetSubLabelText(LocalTTSLexicon data) => data.to.IsNullOrEmpty() ? "<Empty>" : data.to;
}
