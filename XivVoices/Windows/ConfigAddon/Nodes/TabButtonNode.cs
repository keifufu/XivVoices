using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Timelines;

namespace XivVoices.Windows;

public class TabButtonNode : ButtonBase
{
  public required Vector2 StartPosition { get; set; }
  public ConfigTab Tab { get; init; }

  private readonly IconNode _iconNode;

  public TabButtonNode()
  {
    _iconNode = new IconNode
    {
      Size = new(44),
      Position = new(100),
    };
    _iconNode.IconExtras.IsVisible = true;
    _iconNode.AttachNode(this);

    // Fixes HoveredBorderImageNode always being invisible after attaching. I don't understand timelines enough
    // and how the timeline on HoveredBorderImageNode in KTK was meant to be used.
    _iconNode.IconExtras.HoveredBorderImageNode.AddTimeline(new TimelineBuilder().Build());
    IsActive = false;

    AddEvent(AtkEventType.MouseOver, OnHoverStart);
    AddEvent(AtkEventType.MouseOut, OnHoverEnd);

    LoadTwoPartTimelines(this, _iconNode);
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
  }

  public bool IsActive
  {
    get;
    set
    {
      field = value;
      _iconNode.IconExtras.HoveredBorderImageNode.IsVisible = IsActive;
    }
  }

  private void OnHoverStart()
    => _iconNode.IconExtras.HoveredBorderImageNode.IsVisible = IsActive || true;

  private void OnHoverEnd()
    => _iconNode.IconExtras.HoveredBorderImageNode.IsVisible = IsActive || false;

  public uint IconId
  {
    get => _iconNode.IconId;
    set => _iconNode.IconId = value;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();
    _iconNode.Size = Size;
  }
}
