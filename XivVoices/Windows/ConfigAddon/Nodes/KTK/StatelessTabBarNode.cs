using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;
using KamiToolKit.Timelines;
using Lumina.Text.ReadOnly;

namespace XivVoices.Windows;

public class StatelessTabBarNode : SimpleComponentNode
{
  private readonly List<StatlessTabBarRadioButtonNode> _radioButtons = [];

  public StatelessTabBarNode()
  {
    BuildTimelines();
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();
    RecalculateLayout();
  }

  public void AddTab(ReadOnlySeString label, System.Action callback, bool isEnabled = true, bool isSelected = false)
  {
    StatlessTabBarRadioButtonNode newButton = new()
    {
      Height = Height,
      String = label,
      OnClick = callback,
      IsEnabled = isEnabled,
      MultiplyColor = isEnabled ? Vector3.One : new Vector3(0.6f, 0.6f, 0.6f),
      IsSelected = isSelected
    };
    newButton.AddEvent(AtkEventType.ButtonClick, () => newButton.IsChecked = false);

    _radioButtons.Add(newButton);
    newButton.AttachNode(this);

    RecalculateLayout();
  }

  private void RecalculateLayout()
  {
    float step = Width / _radioButtons.Count;

    foreach (int index in Enumerable.Range(0, _radioButtons.Count))
    {
      StatlessTabBarRadioButtonNode button = _radioButtons[index];

      button.Width = step + 5.0f;
      button.X = (step * index) - 5.0f;
      button.Height = Height;
    }
  }

  private void BuildTimelines()
  {
    AddTimeline(new TimelineBuilder()
      .BeginFrameSet(1, 20)
      .AddLabel(1, 101, AtkTimelineJumpBehavior.PlayOnce, 0)
      .AddLabel(11, 102, AtkTimelineJumpBehavior.PlayOnce, 0)
      .EndFrameSet()
      .Build()
    );
  }
}
