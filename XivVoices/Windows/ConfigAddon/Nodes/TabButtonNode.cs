using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Timelines;

namespace XivVoices.Windows;

public class TabButtonNode : ButtonBase
{
  public readonly IconNode IconNode;
  public ConfigTab Tab;

  public required Vector2 StartPosition;

  public TabButtonNode()
  {
    IconNode = new IconNode
    {
      Size = new(44),
      Position = new(100),
    };
    IconNode.IconExtras.IsVisible = true;
    IconNode.AttachNode(this);

    // Fixes HoveredBorderImageNode always being invisible after attaching. I don't understand timelines enough
    // and how the timeline on HoveredBorderImageNode in KTK was meant to be used.
    IconNode.IconExtras.HoveredBorderImageNode.AddTimeline(new TimelineBuilder().Build());
    IsActive = false;

    AddEvent(AtkEventType.MouseOver, OnHoverStart);
    AddEvent(AtkEventType.MouseOut, OnHoverEnd);

    LoadTwoPartTimelines(this, IconNode);
    InitializeComponentEvents();
  }

  public int Index
  {
    get;
    set
    {
      field = value;
      Position = new Vector2(StartPosition.X, StartPosition.Y + (48.0f * Index));
    }
  } = 0;

  public bool IsActive
  {
    get;
    set
    {
      field = value;
      IconNode.IconExtras.HoveredBorderImageNode.IsVisible = IsActive;
    }
  } = false;

  private void OnHoverStart()
    => IconNode.IconExtras.HoveredBorderImageNode.IsVisible = IsActive || true;

  private void OnHoverEnd()
    => IconNode.IconExtras.HoveredBorderImageNode.IsVisible = IsActive || false;

  public uint IconId
  {
    get => IconNode.IconId;
    set => IconNode.IconId = value;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();
    IconNode.Size = Size;
  }
}
