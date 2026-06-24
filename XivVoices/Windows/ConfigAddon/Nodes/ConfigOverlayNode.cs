
using Dalamud.Game.Addon.Events;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class ConfigOverlayNode : ResNode
{
  private readonly IAddonEventManager _addonEventManager;

  public readonly WindowBackgroundNode ContentNode;
  private readonly BackgroundImageNode _configOverlayBackgroundNode;
  private readonly TextNode _titleNode;
  private readonly HorizontalLineNode _horizontalLine;
  private bool _isCursorSet = false;

  public ConfigOverlayNode(IServiceProvider services)
  {
    _addonEventManager = services.GetRequiredService<IAddonEventManager>();

    IsVisible = false;
    Position = new Vector2(-66.0f, -36.0f);

    _configOverlayBackgroundNode = new()
    {
      Color = ColorHelper.GetColor(7),
      Alpha = 0.50f,
    };
    _configOverlayBackgroundNode.AttachNode(this);

    ContentNode = new(false)
    {
      Offsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
      PartsRenderType = 19,
    };
    ContentNode.AttachNode(this);

    _titleNode = new TextNode
    {
      FontType = FontType.TrumpGothic,
      FontSize = 23,
      TextColor = ColorHelper.GetColor(50),
      TextOutlineColor = ColorHelper.GetColor(54),
      TextFlags = TextFlags.Edge,
      String = "XivVoices Overlay",
    };
    _titleNode.AttachNode(ContentNode);

    _horizontalLine = new();
    _horizontalLine.AttachNode(ContentNode);

    unsafe
    {
      _configOverlayBackgroundNode.AddEvent(AtkEventType.MouseMove, (thisPtr, eventType, eventParam, atkEvent, atkEventData) =>
      {

        ref AtkEventData.AtkMouseData mouseData = ref atkEventData->MouseData;
        Vector2 mousePosition = new(mouseData.PosX, mouseData.PosY);

        bool backgroundHovered = _configOverlayBackgroundNode.CheckCollision(atkEventData) && !ContentNode.CheckCollision(atkEventData);

        if (_isCursorSet)
        {
          _addonEventManager.ResetCursor();
          _isCursorSet = false;
        }

        if (backgroundHovered)
        {
          _addonEventManager.SetCursor(AddonCursorType.Clickable);
          _isCursorSet = true;
          atkEvent->SetEventIsHandled(true);
        }
      });

      _configOverlayBackgroundNode.AddEvent(AtkEventType.MouseDown, (thisPtr, eventType, eventParam, atkEvent, atkEventData) =>
      {
        bool backgroundHovered = _configOverlayBackgroundNode.CheckCollision(atkEventData) && !ContentNode.CheckCollision(atkEventData);
        if (backgroundHovered)
        {
          IsVisible = false;
          if (_isCursorSet)
          {
            _addonEventManager.ResetCursor();
            _isCursorSet = false;
          }
        }
      });
    }
  }

  public string Title
  {
    get => _titleNode.String.ToString();
    set => _titleNode.String = value;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _configOverlayBackgroundNode.Size = Size;

    _titleNode.Size = new Vector2(ContentNode.Width - 8.0f, 31.0f);
    _titleNode.Position = new Vector2(12.0f, 7.0f);

    _horizontalLine.Size = new Vector2(ContentNode.Width - 16.0f, 4.0f);
    _horizontalLine.Position = new Vector2(8.0f, _titleNode.Bounds.Bottom + 2.0f);
  }

  public void SetSize(Vector2 size)
  {
    Size = new Vector2(size.X + 72.0f, size.Y + 38.0f);
    PositionContentNode();
  }

  public void PositionContentNode()
  {
    Vector2 newPos = (Size - ContentNode.Size) / 2;
    ContentNode.Position = new Vector2(newPos.X + 32.0f, newPos.Y + 0.0f);
  }

  public void AttachContent(NodeBase node)
  {
    node.Y += 48.0f;
    node.X += 8.0f;
    node.AttachNode(ContentNode);

    float newWidth = node.X + node.Width + 8.0f;
    float newHeight = node.Y + node.Height + 16.0f;

    ContentNode.Size = new Vector2(newWidth > ContentNode.Width ? newWidth : ContentNode.Width, newHeight > ContentNode.Height ? newHeight : ContentNode.Height);

    PositionContentNode();
  }
}

