using KamiToolKit.Premade.Node.Simple;

namespace XivVoices.Windows;

public class ConfigTooltipNode : SimpleImageNode
{
  public ConfigTooltipNode()
  {
    TexturePath = "ui/uld/CircleButtons.tex";
    TextureSize = new Vector2(28.0f, 28.0f);
    TextureCoordinates = new Vector2(112.0f, 84.0f);
    Size = new Vector2(28.0f, 28.0f);
    Scale = new Vector2(0.8f, 0.8f);
    X = 330.0f;
  }
}
