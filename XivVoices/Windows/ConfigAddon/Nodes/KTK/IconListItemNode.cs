using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

// Just IconListItemNode, which was removed with #111
namespace KamiToolKit.Premade.Node.ListItem;

public abstract class IconListItemNode<T> : ListItemNode<T>, IListItemNode
{
  public static float ItemHeight => 48.0f;

  protected readonly IconImageNode IconNode;
  protected readonly TextNode LabelTextNode;
  protected readonly TextNode SubLabelTextNode;
  protected readonly TextNode IdTextNode;

  protected IconListItemNode()
  {
    IconNode = new IconImageNode
    {
      FitTexture = true,
      IconId = 60072,
    };
    IconNode.AttachNode(this);

    LabelTextNode = new TextNode
    {
      TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
      FontSize = 14,
      LineSpacing = 14,
      AlignmentType = AlignmentType.BottomLeft,
      TextColor = ColorHelper.GetColor(8),
      TextOutlineColor = ColorHelper.GetColor(7),
    };
    LabelTextNode.AttachNode(this);

    SubLabelTextNode = new TextNode
    {
      TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
      FontSize = 12,
      LineSpacing = 12,
      AlignmentType = AlignmentType.TopLeft,
      TextColor = ColorHelper.GetColor(3),
      TextOutlineColor = ColorHelper.GetColor(7),
    };
    SubLabelTextNode.AttachNode(this);

    IdTextNode = new TextNode
    {
      TextFlags = TextFlags.Emboss,
      FontSize = 10,
      AlignmentType = AlignmentType.BottomRight,
      TextColor = ColorHelper.GetColor(3),
    };
    IdTextNode.AttachNode(this);

    ShowClickableCursor = true;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    IconNode.Size = new Vector2(Height - 4.0f, Height - 4.0f);
    IconNode.Position = new Vector2(2.0f, 2.0f);

    LabelTextNode.Size = new Vector2(Width - Height - 2.0f - 30.0f, Height / 2.0f);
    LabelTextNode.Position = new Vector2(Height + 2.0f, 0.0f);

    SubLabelTextNode.Size = new Vector2(Width - Height - 2.0f - 10.0f, Height / 2.0f);
    SubLabelTextNode.Position = new Vector2(Height + 2.0f + 10.0f, Height / 2.0f);

    IdTextNode.Size = new Vector2(30.0f, Height / 2.0f);
    IdTextNode.Position = new Vector2(Width - 30.0f, 0.0f);
  }

  protected override void SetNodeData(T itemData)
  {
    IconNode.IconId = GetIconId(itemData);
    LabelTextNode.String = GetLabelText(itemData);
    SubLabelTextNode.String = GetSubLabelText(itemData);
    IdTextNode.String = GetId(itemData).ToString() ?? string.Empty;
  }

  protected abstract uint GetIconId(T data);
  protected abstract string GetLabelText(T data);
  protected abstract string GetSubLabelText(T data);
  protected abstract uint? GetId(T data);
}
