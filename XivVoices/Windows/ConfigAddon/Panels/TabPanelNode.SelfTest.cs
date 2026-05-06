using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

// If we start having enough self-test's to need scrolling, I'd rather just have the stepNodes be scrollable and the buttons/log static.
public class SelfTestTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.SelfTest;
  private ISelfTestService _selfTestService = null!;

  private SelfTestStep _lastStep = SelfTestStep.None;
  private int _lastStepState = -1;

  private readonly List<SelfTestStepNode> _stepNodes = [];
  private TextNode _instructionNode = null!;
  private TextButtonNode _startStopNode = null!;
  private readonly List<TextButtonNode> _actionNodes = [];
  private TextMultiLineInputNode _logNode = null!;

  public override void OnSetup()
  {
    _selfTestService = _services.GetRequiredService<ISelfTestService>();

    foreach (SelfTestStep step in Enum.GetValues<SelfTestStep>())
    {
      if (step == SelfTestStep.None) continue;
      SelfTestStepNode stepNode = new(_selfTestService, step)
      {
        Position = new Vector2(0.0f, 20.0f * _stepNodes.Count)
      };
      AttachNode(stepNode);
      _stepNodes.Add(stepNode);
    }

    _instructionNode = new TextNode()
    {
      Height = 14,
      Position = new Vector2(0.0f, 20.0f * _stepNodes.Count),
      TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
      TextColor = ColorHelper.GetColor(1),
    };
    AttachNode(_instructionNode);

    _startStopNode = new()
    {
      Size = new Vector2(120.0f, 28.0f),
      Position = new Vector2(0.0f, (20.0f * _stepNodes.Count) + 20.0f)
    };
    AttachNode(_startStopNode);

    TextButtonNode actionNode1 = new();
    AttachNode(actionNode1);
    _actionNodes.Add(actionNode1);

    TextButtonNode actionNode2 = new();
    AttachNode(actionNode2);
    _actionNodes.Add(actionNode2);

    TextButtonNode actionNode3 = new();
    AttachNode(actionNode3);
    _actionNodes.Add(actionNode3);

    _logNode = new()
    {
      Position = new Vector2(0.0f, (20.0f * _stepNodes.Count) + 50.0f),
      Size = new Vector2(360.0f, 100.0f),
      Flags = TextInputFlags.MultiLine | TextInputFlags.WordWrap
    };
    AttachNode(_logNode);
  }

  private int _updateCount = 0;
  public override void OnUpdate()
  {
    if (_selfTestService.Step != _lastStep || _selfTestService.StepState != _lastStepState)
    {
      _lastStep = _selfTestService.Step;
      _lastStepState = _selfTestService.StepState;

      foreach (SelfTestStepNode node in _stepNodes)
      {
        node.Active = _selfTestService.Step == node.Step;
        node.Skipped = (_selfTestService.SkippedTests & node.Step) == node.Step;
        node.Completed = (_selfTestService.CompletedTests & node.Step) == node.Step;
      }

      _instructionNode.String = _selfTestService.CurrentInstruction;

      bool isStarted = _selfTestService.Step != SelfTestStep.None;
      _startStopNode.String = (isStarted ? "Stop" : "Start") + " Self-Test";
      _startStopNode.OnClick = () =>
      {
        if (isStarted) _selfTestService.Stop();
        else _selfTestService.Start();
      };

      List<(string button, bool enabled, System.Action action)> buttons = _selfTestService.GetButtonsForCurrentStep();

      for (int i = 0; i < _actionNodes.Count; i++)
      {
        if (i >= buttons.Count)
        {
          _actionNodes[i].IsVisible = false;
          continue;
        }

        TextButtonNode prevNode = (i == 0) ? _startStopNode : _actionNodes[i - 1];

        (string button, bool enabled, System.Action action) = buttons.ElementAt(i);
        _actionNodes[i].IsVisible = true;
        _actionNodes[i].String = button;
        _actionNodes[i].IsEnabled = enabled;
        _actionNodes[i].OnClick = action;
        _actionNodes[i].Size = new Vector2(_actionNodes[i].LabelNode.GetTextDrawSize(false).X + 30.0f, 28.0f);
        _actionNodes[i].Position = new Vector2(prevNode.Bounds.Right, _startStopNode.Position.Y);
      }
    }


    _updateCount++;
    if (_updateCount % 30 == 0)
    {
      string l = "";
      lock (_selfTestService.CurrentLogsLock)
      {
        foreach (string log in _selfTestService.CurrentLogs) l += $"{log}\r";
      }
      if (_logNode.String != l)
        _logNode.String = l;
    }
  }
}
