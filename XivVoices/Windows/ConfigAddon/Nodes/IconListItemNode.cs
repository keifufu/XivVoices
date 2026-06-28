using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public abstract class IconListItemNode<T> : ListItemNode<T>, IListItemNode
{
  public static float ItemHeight => 48.0f;

  protected readonly IconImageNode _iconNode;
  protected readonly TextNode _labelTextNode;
  protected readonly TextNode _subLabelTextNode;

  protected IconListItemNode()
  {
    _iconNode = new IconImageNode
    {
      FitTexture = true,
      IconId = 60072,
    };
    _iconNode.AttachNode(this);

    _labelTextNode = new TextNode
    {
      TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
      FontSize = 14,
      LineSpacing = 14,
      AlignmentType = AlignmentType.BottomLeft,
      TextColor = ColorHelper.GetColor(8),
      TextOutlineColor = ColorHelper.GetColor(7),
    };
    _labelTextNode.AttachNode(this);

    _subLabelTextNode = new TextNode
    {
      TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
      FontSize = 12,
      LineSpacing = 12,
      AlignmentType = AlignmentType.TopLeft,
      TextColor = ColorHelper.GetColor(3),
      TextOutlineColor = ColorHelper.GetColor(7),
    };
    _subLabelTextNode.AttachNode(this);

    ShowClickableCursor = true;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _iconNode.Size = new Vector2(Height - 4.0f, Height - 4.0f);
    _iconNode.Position = new Vector2(2.0f, 2.0f);

    _labelTextNode.Size = new Vector2(Width - Height - 2.0f - 30.0f, Height / 2.0f);
    _labelTextNode.Position = new Vector2(Height + 2.0f, 0.0f);

    _subLabelTextNode.Size = new Vector2(Width - Height - 2.0f - 10.0f, Height / 2.0f);
    _subLabelTextNode.Position = new Vector2(Height + 2.0f + 10.0f, Height / 2.0f);
  }

  protected override void SetNodeData(T itemData)
  {
    _iconNode.IconId = GetIconId(itemData);
    _labelTextNode.String = GetLabelText(itemData);
    _subLabelTextNode.String = GetSubLabelText(itemData);
  }

  protected abstract uint GetIconId(T data);
  protected abstract string GetLabelText(T data);
  protected abstract string GetSubLabelText(T data);
}
