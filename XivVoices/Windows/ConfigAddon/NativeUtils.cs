using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public static class NativeUtils
{
  public static void FixSliderNode(SliderNode node)
  {
    node.SliderBackgroundButtonNode.BackgroundTexture.Size = new Vector2(node.Width - 44.0f, node.Height / 2.0f);
    node.SliderBackgroundButtonNode.BackgroundTexture.Y = 0;
    node.SliderForegroundButtonNode.Size = new Vector2(node.Height, node.Height);
    node.SliderForegroundButtonNode.HandleNode.Position = new Vector2(1.0f, 1.0f);
    node.SliderForegroundButtonNode.HandleNode.WrapMode = WrapMode.Tile;
    node.ValueNode.TextColor = ColorHelper.GetColor(2);
  }
}
