using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Premade.Node.Simple;
using KamiToolKit.Timelines;
using Lumina.Text.ReadOnly;

namespace KamiToolKit.Nodes;

public unsafe class StatelessTabBarRadioButtonNode : ComponentNode<AtkComponentRadioButton, AtkUldComponentDataRadioButton>
{

  public readonly TextNode LabelNode;
  public readonly NineGridNode SelectedNineGridNode;
  public readonly NineGridNode UnselectedNineGridNode;

  public StatelessTabBarRadioButtonNode()
  {
    SetInternalComponentType(ComponentType.RadioButton);

    UnselectedNineGridNode = new SimpleNineGridNode
    {
      Position = new Vector2(-2.0f, -1.0f),
      TexturePath = "ui/uld/TabButtonA.tex",
      TextureCoordinates = new Vector2(0.0f, 0.0f),
      TextureSize = new Vector2(88.0f, 26.0f),
      LeftOffset = 16,
      RightOffset = 16,
    };
    UnselectedNineGridNode.AttachNode(this);

    SelectedNineGridNode = new SimpleNineGridNode
    {
      Position = new Vector2(-2.0f, -1.0f),
      TexturePath = "ui/uld/TabButtonA.tex",
      TextureCoordinates = new Vector2(0.0f, 26.0f),
      TextureSize = new Vector2(88.0f, 26.0f),
      LeftOffset = 16,
      RightOffset = 16,
      IsVisible = false,
    };
    SelectedNineGridNode.AttachNode(this);

    LabelNode = new TextNode
    {
      Position = new Vector2(13.0f, 2.0f),
      AlignmentType = AlignmentType.Center,
      TextColor = ColorHelper.GetColor(50),
    };
    LabelNode.AttachNode(this);

    BuildTimelines();

    Data->Nodes[0] = LabelNode.NodeId;
    Data->Nodes[1] = UnselectedNineGridNode.NodeId;
    Data->Nodes[2] = 0;
    Data->Nodes[3] = 0;

    AddEvent(AtkEventType.ButtonClick, ClickHandler);
    AddEvent(AtkEventType.MouseOver, OnHoverStart);
    AddEvent(AtkEventType.MouseOut, OnHoverEnd);

    InitializeComponentEvents();
  }

  private void OnHoverStart()
    => AddColor = new Vector3(16.0f / 255.0f);

  private void OnHoverEnd()
    => AddColor = new Vector3(0.0f);

  public System.Action? OnClick { get; set; }

  public ReadOnlySeString String
  {
    get => LabelNode.String;
    set => Component->SetText(value);
  }

  public bool IsSelected
  {
    get => Component->IsSelected;
    set
    {
      Component->IsSelected = value;
      if (value)
      {
        SelectedNineGridNode.IsVisible = true;
        UnselectedNineGridNode.IsVisible = false;
      }
      else
      {
        SelectedNineGridNode.IsVisible = false;
        UnselectedNineGridNode.IsVisible = true;
      }
    }
  }

  public bool IsChecked
  {
    get => Component->IsChecked;
    set => Component->SetChecked(value);
  }

  private void ClickHandler()
  {
    OnClick?.Invoke();
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    CollisionNode.Size = Size;
    UnselectedNineGridNode.Size = new Vector2(Width + 4.0f, Height + 2.0f);
    SelectedNineGridNode.Size = new Vector2(Width + 4.0f, Height + 2.0f);
    LabelNode.Size = new Vector2(Width - 25.0f, Height - 4.0f);
  }

  private void BuildTimelines()
  {
    AddTimeline(new TimelineBuilder()
        .BeginFrameSet(11, 20)
        .AddFrame(11, new Vector2(525, 0))
        .EndFrameSet()
        .Build()
    );

    UnselectedNineGridNode.AddTimeline(new TimelineBuilder()
        .BeginFrameSet(1, 9)
        .AddFrame(1, alpha: 255)
        .AddFrame(1, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(10, 19)
        .AddFrame(10, alpha: 255)
        .AddFrame(12, alpha: 255)
        .AddFrame(10, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(12, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(20, 29)
        .AddFrame(20, alpha: 255)
        .AddFrame(20, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(30, 39)
        .AddFrame(30, alpha: 178)
        .AddFrame(30, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(50, 50, 50))
        .EndFrameSet()
        .BeginFrameSet(40, 49)
        .AddFrame(40, alpha: 255)
        .AddFrame(40, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(50, 59)
        .AddFrame(50, alpha: 255)
        .AddFrame(52, alpha: 255)
        .AddFrame(50, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(52, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(120, 129)
        .AddFrame(120, alpha: 255)
        .AddFrame(122, alpha: 0)
        .AddFrame(120, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(122, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(130, 139)
        .AddFrame(130, alpha: 0)
        .AddFrame(132, alpha: 255)
        .AddFrame(130, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(132, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(140, 149)
        .AddFrame(140, alpha: 255)
        .AddFrame(142, alpha: 0)
        .AddFrame(140, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(142, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(150, 159)
        .AddFrame(150, alpha: 0)
        .AddFrame(152, alpha: 255)
        .AddFrame(150, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(152, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .Build()
    );

    SelectedNineGridNode.AddTimeline(new TimelineBuilder()
        .BeginFrameSet(60, 69)
        .AddFrame(60, alpha: 255)
        .AddFrame(60, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(70, 79)
        .AddFrame(70, alpha: 255)
        .AddFrame(72, alpha: 255)
        .AddFrame(70, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(72, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(80, 89)
        .AddFrame(80, alpha: 255)
        .AddFrame(80, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(90, 99)
        .AddFrame(90, alpha: 178)
        .AddFrame(90, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(50, 50, 50))
        .EndFrameSet()
        .BeginFrameSet(100, 109)
        .AddFrame(100, alpha: 255)
        .AddFrame(100, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(110, 119)
        .AddFrame(110, alpha: 255)
        .AddFrame(112, alpha: 255)
        .AddFrame(110, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(112, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(120, 129)
        .AddFrame(120, alpha: 0)
        .AddFrame(122, alpha: 255)
        .AddFrame(120, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(122, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(130, 139)
        .AddFrame(130, alpha: 255)
        .AddFrame(132, alpha: 0)
        .AddFrame(130, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(132, addColor: new Vector3(16, 16, 16), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(140, 149)
        .AddFrame(140, alpha: 0)
        .AddFrame(142, alpha: 255)
        .AddFrame(140, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(142, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(150, 159)
        .AddFrame(150, alpha: 255)
        .AddFrame(152, alpha: 0)
        .AddFrame(150, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .AddFrame(152, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .Build()
    );

    LabelNode.AddTimeline(new TimelineBuilder()
        .BeginFrameSet(1, 9)
        .AddFrame(1, alpha: 255)
        .AddFrame(1, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(10, 19)
        .AddFrame(10, alpha: 255)
        .AddFrame(10, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(20, 29)
        .AddFrame(20, alpha: 255)
        .AddFrame(20, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(30, 39)
        .AddFrame(30, alpha: 153)
        .AddFrame(30, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(80, 80, 80))
        .EndFrameSet()
        .BeginFrameSet(40, 49)
        .AddFrame(40, alpha: 255)
        .AddFrame(40, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(50, 59)
        .AddFrame(50, alpha: 255)
        .AddFrame(50, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(60, 69)
        .AddFrame(60, alpha: 255)
        .AddFrame(60, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(70, 79)
        .AddFrame(70, alpha: 255)
        .AddFrame(70, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(80, 89)
        .AddFrame(80, alpha: 255)
        .AddFrame(80, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(90, 99)
        .AddFrame(90, alpha: 153)
        .AddFrame(90, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(80, 80, 80))
        .EndFrameSet()
        .BeginFrameSet(100, 109)
        .AddFrame(100, alpha: 255)
        .AddFrame(100, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(110, 119)
        .AddFrame(110, alpha: 255)
        .AddFrame(110, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(120, 129)
        .AddFrame(120, alpha: 255)
        .AddFrame(120, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(130, 139)
        .AddFrame(130, alpha: 255)
        .AddFrame(130, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(140, 149)
        .AddFrame(140, alpha: 255)
        .AddFrame(140, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .BeginFrameSet(150, 159)
        .AddFrame(150, alpha: 255)
        .AddFrame(150, addColor: new Vector3(0, 0, 0), multiplyColor: new Vector3(100, 100, 100))
        .EndFrameSet()
        .Build()
    );
  }
}
