using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes.Simplified;

namespace XivVoices.Windows;

public abstract class TabPanelNode : SimpleComponentNode
{
  public bool SetupComplete = false;

  protected TabPanelNode()
  {
    OnSetup();
    SetupComplete = true;
    ConfigurationSaved();
  }

  protected override void Dispose(bool disposing, bool isNativeDestructor)
  {
    base.Dispose(disposing, isNativeDestructor);
    SetupComplete = false;
  }

  internal void AttachNode(NodeBase node)
  {
    node.AttachNode(this);
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
  public Action<ConfigTab>? SetTab;
}
