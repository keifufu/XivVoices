using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;

// SelectableNode but OnClick passes isRightClick, and selection is disabled.
namespace XivVoices.Windows;

public class AudioListSelectableNode : SimpleComponentNode
{
  private readonly NineGridNode _hoveredBackgroundNode;
  private readonly NineGridNode _selectedBackgroundNode;

  public AudioListSelectableNode()
  {
    _hoveredBackgroundNode = new SimpleNineGridNode
    {
      TexturePath = "ui/uld/ListItemA.tex",
      TextureCoordinates = new Vector2(0.0f, 22.0f),
      TextureSize = new Vector2(64.0f, 22.0f),
      TopOffset = 6,
      BottomOffset = 6,
      LeftOffset = 16,
      RightOffset = 1,
      IsVisible = false,
    };
    _hoveredBackgroundNode.AttachNode(this);

    _selectedBackgroundNode = new SimpleNineGridNode
    {
      TexturePath = "ui/uld/ListItemA.tex",
      TextureCoordinates = new Vector2(0.0f, 0.0f),
      TextureSize = new Vector2(64.0f, 22.0f),
      TopOffset = 6,
      BottomOffset = 6,
      LeftOffset = 16,
      RightOffset = 1,
      IsVisible = false,
    };
    _selectedBackgroundNode.AttachNode(this);

    CollisionNode.AddEvent(AtkEventType.MouseOver, () =>
    {
      if (!IsSelected && EnableHighlight)
      {
        IsHovered = true;
      }
    });

    unsafe
    {
      CollisionNode.AddEvent(AtkEventType.MouseDown, (thisPtr, eventType, eventParam, atkEvent, atkEventData) =>
      {
        if (EnableSelection)
        {
          // IsSelected = !IsSelected;
          OnClick?.Invoke(this, atkEventData->MouseData.ButtonId == 1);
        }
      });
    }

    CollisionNode.AddEvent(AtkEventType.MouseOut, () =>
    {
      IsHovered = false;
    });

    CollisionNode.AddDrawFlags(DrawFlags.ClickableCursor);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _hoveredBackgroundNode.Size = Size + new Vector2(6.0f, 6.0f);
    _hoveredBackgroundNode.Position = new Vector2(-3.0f, -3.0f);

    _selectedBackgroundNode.Size = Size + new Vector2(6.0f, 6.0f);
    _selectedBackgroundNode.Position = new Vector2(-3.0f, -3.0f);
  }

  public Action<AudioListSelectableNode, bool>? OnClick
  {
    get;
    set
    {
      field = value;
      CollisionNode.ShowClickableCursor = value is not null && EnableSelection;
    }
  }

  public bool EnableSelection
  {
    get;
    set
    {
      field = value;
      CollisionNode.ShowClickableCursor = value;
    }
  } = true;

  public bool EnableHighlight { get; set; } = true;

  public bool IsHovered
  {
    get => _hoveredBackgroundNode.IsVisible;
    set => _hoveredBackgroundNode.IsVisible = value;
  }

  public bool IsSelected
  {
    get => _selectedBackgroundNode.IsVisible;
    set
    {
      _selectedBackgroundNode.IsVisible = value;

      if (value)
      {
        _hoveredBackgroundNode.IsVisible = false;
      }
    }
  }
}
