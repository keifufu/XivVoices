using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public class SelfTestStepNode : ResNode
{
  public SelfTestStep Step;

  private readonly TextNode _stateNode;

  public SelfTestStepNode(ISelfTestService _selfTestService, SelfTestStep step)
  {
    Step = step;

    TextNode textNode = new()
    {
      String = Step.ToString(),
      Height = 12.0f,
    };
    textNode.AttachNode(this);

    _stateNode = new()
    {
      Height = 12.0f,
      Position = new Vector2(250.0f, 0.0f),
    };
    _stateNode.AttachNode(this);

    CircleButtonNode skipNode = new()
    {
      Icon = ButtonIcon.RightArrow,
      Size = new Vector2(24.0f, 24.0f),
      Position = new Vector2(330.0f, -4.0f),
      TextTooltip = $"Skip to {Step}",
      OnClick = () => _selfTestService.SkipTo(Step),
    };
    skipNode.AttachNode(this);

    UpdateState();
  }

  private void UpdateState()
  {
    _stateNode.String = Active ? "Running" : Skipped ? "Skipped" : Completed ? "Completed" : "Pending";
    _stateNode.TextColor = ColorHelper.GetColor((uint)(Active ? 25 : Skipped ? 3 : Completed ? 45 : 2));
  }

  private bool _active = false;
  public bool Active
  {
    get => _active;
    set
    {
      _active = value;
      UpdateState();
    }
  }

  private bool _skipped = false;
  public bool Skipped
  {
    get => _skipped;
    set
    {
      _skipped = value;
      UpdateState();
    }
  }

  private bool _completed = false;
  public bool Completed
  {
    get => _completed;
    set
    {
      _completed = value;
      UpdateState();
    }
  }
}
