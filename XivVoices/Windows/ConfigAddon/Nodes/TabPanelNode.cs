using KamiToolKit;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;

namespace XivVoices.Windows;

public abstract class TabPanelNode : SimpleComponentNode
{
  public readonly ScrollingAreaNode<ResNode>? _containerNode = null;

  protected TabPanelNode(bool container)
  {
    if (container)
    {
      _containerNode = new()
      {
        ContentHeight = 0.0f,
        AutoHideScrollBar = true,
      };

      _containerNode.AttachNode(this);
    }

    OnSetup();
    ConfigurationSaved();
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _containerNode?.Size = Size;
  }

  internal void AttachNode(NodeBase node)
  {
    if (_containerNode == null)
    {
      node.AttachNode(this);
    }
    else
    {
      node.AttachNode(_containerNode.ContentNode);
      _containerNode.ContentHeight = node.Y + node.Height + 5.0f;
    }
  }

  public bool IsActive
  {
    get => IsVisible;
    set
    {
      IsVisible = value;
      ConfigurationSaved();
    }
  }

  public abstract ConfigTab Tab { get; }
  public virtual void OnSetup() { }
  public virtual void ConfigurationSaved() { }
  public virtual void OnUpdate() { }
}
