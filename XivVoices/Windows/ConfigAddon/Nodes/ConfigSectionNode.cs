using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public class ConfigSectionNode : ResNode
{
  public const float Indent = 18.0f;

  private float _lastNodeY = 0.0f;
  private float _currentY = 0.0f;

  public ConfigSectionNode(string header, ConfigSectionNode? previousSection = null, float offset = 0)
  {
    Y = previousSection == null ? 0.0f : previousSection.Y + previousSection.Height + 0.0f;
    Y += offset;

    AttachNode(new CategoryTextNode()
    {
      String = header,
    }, indent: false);
  }

  public void AttachNode(NodeBase node, float padding = 0.0f, bool inline = false, bool indent = true)
  {
    if (inline)
    {
      if (node.Y == 0.0f) node.Y = _lastNodeY + padding;
    }
    else
    {
      if (indent) node.X += Indent;
      node.Y += _currentY + padding;
      _lastNodeY = node.Y;
      _currentY = node.Y + node.Height;
    }
    node.AttachNode(this);

    Height = node.Y + node.Height + 5.0f;
    float newWidth = node.X + node.Width;
    if (newWidth > Width) Width = newWidth;
  }
}
