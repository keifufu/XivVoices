using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public abstract class TabPanelNode : ResNode
{
  protected TabPanelNode()
  {
    OnSetup();
  }

  internal void AttachNode(NodeBase node) => node.AttachNode(this);

  public bool IsActive
  {
    get => IsVisible;
    set
    {
      if (value) ConfigurationSaved();
      IsVisible = value;
    }
  }

  public abstract ConfigTab Tab { get; }
  public virtual void OnSetup() { }
  public virtual void ConfigurationSaved() { }
  public virtual void OnUpdate() { }
  public Action<ConfigTab>? SetTab;
}
