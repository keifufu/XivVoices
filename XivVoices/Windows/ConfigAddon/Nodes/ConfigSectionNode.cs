using KamiToolKit;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class ConfigSectionNode : ResNode
{
  public const float Indent = 18.0f;

  public float LastNodeY = 0.0f;
  public float CurrentY = 0.0f;

  public ConfigSectionNode(string header, ConfigSectionNode? previousSection = null)
  {
    Y = previousSection == null ? 0.0f : previousSection.Y + previousSection.Height + 5.0f;

    AttachNode(new CategoryTextNode()
    {
      String = header,
    }, indent: false);
  }

  public void AttachNode(NodeBase node, float padding = 0.0f, bool inline = false, bool indent = true)
  {
    if (inline)
    {
      if (node.Y == 0.0f) node.Y = LastNodeY + padding;
    }
    else
    {
      if (indent) node.X += Indent;
      node.Y += CurrentY + padding;
      LastNodeY = node.Y;
      CurrentY = node.Y + node.Height;
    }
    node.AttachNode(this);

    Height = node.Y + node.Height + 5.0f;
    float newWidth = node.X + node.Width;
    if (newWidth > Width) Width = newWidth;
  }
}
