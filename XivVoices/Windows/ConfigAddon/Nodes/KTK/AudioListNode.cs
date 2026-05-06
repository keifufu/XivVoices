using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Premade.Node.Simple;

namespace KamiToolKit.Nodes;

internal static class GenericUtil
{
  public static bool AreEqual<T>(T? left, T? right)
  {
    if (default(T) == null) return ReferenceEquals(left, right);

    if (left == null || right == null) return left == null && right == null;

    var leftSpan = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref left), Unsafe.SizeOf<T>());
    var rightSpan = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref right), Unsafe.SizeOf<T>());

    return leftSpan.SequenceEqual(rightSpan);
  }
}

public unsafe class AudioListNode<T, TU> : SimpleComponentNode where TU : AudioListItemNode<T>, IAudioListItemNode, new()
{
  public readonly ScrollBarNode ScrollBarNode;

  public AudioListNode()
  {
    itemHeight = TU.ItemHeight;

    ScrollBarNode = new ScrollBarNode
    {
      OnValueChanged = OnScrollUpdate,
      ScrollSpeed = (int)itemHeight,
      HideWhenDisabled = true,
    };
    ScrollBarNode.AttachNode(this);

    AddEvent(AtkEventType.MouseWheel, OnMouseWheel);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    ScrollBarNode.Size = new Vector2(8.0f, Height);
    ScrollBarNode.Position = new Vector2(Width - 8.0f, 0.0f);

    var newNodeCount = (int)(Height / (itemHeight + ItemSpacing));
    if (newNodeCount != nodeCount)
    {
      FullRebuild();
    }

    foreach (var node in nodeList)
    {
      node.Width = ScrollBarNode.Bounds.Left - 8.0f;
    }

    RecalculateScroll();
  }

  public Action<T?, bool>? OnItemSelected { get; set; }

  public float ItemSpacing
  {
    get;
    set
    {
      field = value;
      FullRebuild();
    }
  }

  public required List<T> OptionsList
  {
    get;
    set
    {
      field = value;

      var newNodeCount = (int)(Height / (itemHeight + ItemSpacing));
      if (newNodeCount != nodeCount)
      {
        FullRebuild();
      }
      else
      {
        PopulateNodes();
        RecalculateScroll();
      }
    }
  } = [];

  private readonly List<TU> nodeList = [];
  private readonly float itemHeight;
  private T? selectedItem;
  private int scrollPosition;
  private int nodeCount;

  /// <summary>
  /// Resets and rebuilds list
  /// </summary>
  public void FullRebuild()
  {
    foreach (var node in nodeList)
    {
      node.Dispose();
    }
    nodeList.Clear();

    scrollPosition = Math.Clamp(scrollPosition, 0, Math.Max(OptionsList.Count - nodeCount, 0));
    selectedItem = default;

    RebuildNodeList();
    PopulateNodes();
    RecalculateScroll();
  }

  public void Update()
  {
    PopulateNodes();

    foreach (var node in nodeList)
    {
      if (node.IsVisible)
      {
        node.Update();
      }
    }
  }

  private void RebuildNodeList()
  {
    nodeCount = (int)(Height / (itemHeight + ItemSpacing));
    if (nodeCount < 1) return;

    foreach (var index in Enumerable.Range(0, nodeCount))
    {
      var node = new TU
      {
        Size = new Vector2(ScrollBarNode.Bounds.Left - 8.0f, itemHeight),
        Position = new Vector2(0.0f, index * (itemHeight + ItemSpacing)),
        NodeId = (uint)index + 2,
        OnClick = (clickedNode, isRightClick) =>
        {
          SelectItem(((TU)clickedNode).ItemData);
          OnItemSelected?.Invoke(selectedItem, isRightClick);
        },
        IsVisible = false,
      };
      node.AttachNode(this);
      nodeList.Add(node);
    }
  }

  private void PopulateNodes()
  {
    foreach (var (nodeIndex, node) in nodeList.Index())
    {
      var dataIndex = scrollPosition + nodeIndex;

      if (dataIndex < OptionsList.Count)
      {
        var item = OptionsList[dataIndex];
        node.ItemData = item;
        node.IsVisible = true;
        node.IsSelected = false;
      }
      else
      {
        node.IsVisible = false;
      }
    }
  }

  private void SelectItem(T? item)
  {
    if (item is null) return;

    selectedItem = item;

    foreach (var node in nodeList)
    {
      if (node.ItemData is null)
      {
        node.IsSelected = false;
      }
      else
      {
        node.IsSelected = false;
      }
    }
  }

  private void RecalculateScroll()
  {
    if (OptionsList.Count < nodeCount)
    {
      ScrollBarNode.ScrollPosition = 0;
      ScrollBarNode.IsEnabled = false;
    }

    var totalHeight = (int)(OptionsList.Count * (itemHeight + ItemSpacing) + ItemSpacing);
    ScrollBarNode.UpdateScrollParams((int)(nodeList.Count * (itemHeight + ItemSpacing)), totalHeight);
    ScrollBarNode.ScrollPosition = (int)(scrollPosition * (itemHeight + ItemSpacing));
  }

  private void OnScrollUpdate(int newPosition)
  {
    scrollPosition = (int)(newPosition / (itemHeight + ItemSpacing));
    PopulateNodes();
  }

  private void OnMouseWheel(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
  {
    scrollPosition += atkEventData->MouseData.WheelDirection >= 1 ? -1 : 1;
    scrollPosition = Math.Clamp(scrollPosition, 0, Math.Max(0, OptionsList.Count - nodeCount));

    ScrollBarNode.ScrollPosition = (int)(scrollPosition * (itemHeight + ItemSpacing));
    PopulateNodes();

    atkEvent->SetEventIsHandled();
  }
}
