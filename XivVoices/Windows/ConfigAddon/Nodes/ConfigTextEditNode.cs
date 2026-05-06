using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class ConfigTextEditNode : ResNode
{
  private readonly CircleButtonNode _buttonNode;
  private readonly LabelTextNode _labelNode;
  private readonly string _name;

  public ConfigTextEditNode(string name)
  {
    _name = name;

    _buttonNode = new()
    {
      Icon = ButtonIcon.EditSmall,
      OnClick = () => OnClick?.Invoke(),
    };
    _buttonNode.AttachNode(this);

    _labelNode = new()
    {
      FontSize = 14,
      Height = 16.0f,
      TextFlags = FFXIVClientStructs.FFXIV.Component.GUI.TextFlags.Ellipsis,
    };
    _labelNode.AttachNode(this);
  }

  public System.Action? OnClick { get; set; }

  public string Value
  {
    get => _labelNode.String.ToString().Replace($"{_name}: ", "");
    set => _labelNode.String = $"{_name}: {value}";
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _buttonNode.Position = new Vector2(-3.0f, 0.0f);
    _buttonNode.Size = new Vector2(24.0f, 24.0f);

    _labelNode.Size = new Vector2(330.0f, 24.0f);
    _labelNode.Position = new Vector2(_buttonNode.Bounds.Right + 1.0f, 0.0f);
  }
}
