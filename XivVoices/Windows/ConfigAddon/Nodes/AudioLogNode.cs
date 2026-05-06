using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public class AudioLogNode : AudioListItemNode<(XivMessage message, bool isPlaying, float percentage, bool isQueued)>, IAudioListItemNode
{
  public static float ItemHeight => 36.0f;

  private readonly TextNode _textNode;
  private readonly ProgressBarNode _progressBarNode;

  public AudioLogNode()
  {
    _textNode = new()
    {
      Position = new Vector2(0.0f, 0.0f),
      TextFlags = TextFlags.Ellipsis,
    };
    _textNode.AttachNode(this);

    _progressBarNode = new()
    {
      DisableCollisionNode = true,
    };
    _progressBarNode.AttachNode(this);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _progressBarNode.Size = new Vector2(Width, 20.0f);
    _progressBarNode.Position = new Vector2(0.0f, Height - _progressBarNode.Height);
    _textNode.Size = new Vector2(Width, 16.0f);
  }

  protected override void SetNodeData((XivMessage message, bool isPlaying, float percentage, bool isQueued) itemData)
  {
    string message = $"{itemData.message.RawSpeaker}: {itemData.message.RawSentence}";
    TextTooltip = message;
    _textNode.String = message;
    _progressBarNode.Progress = itemData.percentage;
    _progressBarNode.BarColor = ColorHelper.GetColor(itemData.message.IsLocalTTS ? 25u : 45u);
    _progressBarNode.BackgroundColor = ColorHelper.GetColor(4);
    _progressBarNode.BackgroundNode.IsVisible = itemData.isQueued;
  }
}
