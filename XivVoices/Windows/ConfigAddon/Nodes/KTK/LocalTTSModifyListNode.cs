using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using KamiToolKit.Premade.Node.Simple;

namespace XivVoices.Windows;

public class LocalTTSModifyListNode<T, TU> : SimpleComponentNode where T : struct where TU : ListItemNode<T>, IListItemNode, new()
{
  private readonly IKeyState _keyState;
  private readonly SearchWidget _searchWidget;
  private readonly CircleButtonNode _exportButton;
  private readonly ListNode<T, TU> _listNode;

  private readonly TextButtonNode _addButton;
  private readonly TextButtonNode _editButton;
  private readonly TextButtonNode _removeButton;

  public LocalTTSModifyListNode(IKeyState keyState)
  {
    _keyState = keyState;
    _searchWidget = new SearchWidget
    {
      OnSortOrderChanged = OnSortOrderChanged,
      OnSearchUpdated = OnSearchUpdated,
    };
    _searchWidget.InputNode.PlaceholderString = "Search ...";
    _searchWidget.AttachNode(this);

    _exportButton = new CircleButtonNode()
    {
      Size = new Vector2(30.0f, 30.0f),
      OnClick = () =>
      {
        if (_exportButton?.Icon == ButtonIcon.CheckedBox || _exportButton?.Icon == ButtonIcon.Cross) return;

        (string result, bool success) result;
        if (_exportButton?.Icon == ButtonIcon.Undo)
        {
          result = OnUndo?.Invoke() ?? default;
          _lastWasImport = false;
        }
        else if (_keyState[VirtualKey.SHIFT])
        {
          result = OnImport?.Invoke(_keyState[VirtualKey.CONTROL]) ?? default;
          _lastWasImport = true;
        }
        else
        {
          result = OnExport?.Invoke() ?? default;
          _lastWasImport = false;
        }
        _lastImportExport = DateTime.Now;
        _lastImportExportResult = result.result;
        _lastImportExportSuccess = result.success;
      }
    };
    _exportButton.AttachNode(this);
    _exportButton.AddEvent(AtkEventType.MouseOver, MouseOver);
    _exportButton.AddEvent(AtkEventType.MouseOut, MouseOut);

    _listNode = new ListNode<T, TU>
    {
      OptionsList = [],
      OnItemSelected = OnListItemSelected,
    };
    _listNode.AttachNode(this);

    _addButton = new TextButtonNode
    {
      String = "Add",
      OnClick = OnAddClicked,
    };
    _addButton.AttachNode(this);

    _editButton = new TextButtonNode
    {
      String = "Edit",
      OnClick = OnEditClicked,
    };
    _editButton.AttachNode(this);

    _removeButton = new TextButtonNode
    {
      String = "Remove",
      OnClick = OnRemoveClicked,
    };
    _removeButton.AttachNode(this);

    UpdateButtonStates();
  }

  private DateTime _lastImportExport = new();
  private bool _lastImportExportSuccess = false;
  private string _lastImportExportResult = "";
  private bool _lastWasImport = false;
  public Func<bool, (string result, bool success)>? OnImport { get; init; }
  public Func<(string result, bool success)>? OnExport { get; init; }
  public Func<(string result, bool success)>? OnUndo { get; init; }

  bool tooltipVisible = false;
  private void MouseOver() => tooltipVisible = true;
  private void MouseOut() => tooltipVisible = false;

  public void OnUpdate()
  {
    bool showImportExportResult = (DateTime.Now - _lastImportExport).TotalSeconds <= 3;
    ButtonIcon importExportResultIcon = _lastImportExportSuccess ? ButtonIcon.CheckedBox : ButtonIcon.Cross;
    if (showImportExportResult && _lastImportExportSuccess && _lastWasImport && _exportButton.Icon != ButtonIcon.Undo)
    {
      _exportButton.Icon = ButtonIcon.Undo;
      _exportButton.TextTooltip = _lastImportExportResult + " Click to undo.";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
    else if (showImportExportResult && _exportButton.Icon != importExportResultIcon && !(_lastWasImport && _lastImportExportSuccess))
    {
      _exportButton.Icon = importExportResultIcon;
      _exportButton.TextTooltip = _lastImportExportResult;
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
    else if (!showImportExportResult && _keyState[VirtualKey.SHIFT] && _keyState[VirtualKey.CONTROL] && !_exportButton.TextTooltip.ToString().Contains("(Override duplicates)"))
    {
      _exportButton.Icon = ButtonIcon.Add;
      _exportButton.TextTooltip = "Import (Override duplicates)";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
    else if (!showImportExportResult && _keyState[VirtualKey.SHIFT] && !_keyState[VirtualKey.CONTROL] && !_exportButton.TextTooltip.ToString().Contains("(Hold Control"))
    {
      _exportButton.Icon = ButtonIcon.Add;
      _exportButton.TextTooltip = "Import (Hold Control to override duplicates)";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
    else if (!showImportExportResult && !_keyState[VirtualKey.SHIFT] && !_keyState[VirtualKey.CONTROL] && _exportButton.Icon != ButtonIcon.Document)
    {
      _exportButton.Icon = ButtonIcon.Document;
      _exportButton.TextTooltip = "Export (Hold Shift to Import)";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _searchWidget.Size = new Vector2(Width - _exportButton.Width - 4.0f, 30.0f);
    _searchWidget.Position = Vector2.Zero;

    _exportButton.Position = new Vector2(_searchWidget.Bounds.Right, 5.0f);

    _listNode.Size = new Vector2(Width, Height - _searchWidget.Height - 40.0f);
    _listNode.Position = new Vector2(0.0f, _searchWidget.Y + _searchWidget.Height + 8.0f);

    const float buttonPadding = 5.0f;
    float buttonWidth = (Width - (buttonPadding * 2.0f)) / 3.0f;

    _addButton.Size = new Vector2(buttonWidth, 28.0f);
    _addButton.Position = new Vector2(0.0f, Height - 28.0f);

    _editButton.Size = new Vector2(buttonWidth, 28.0f);
    _editButton.Position = new Vector2(buttonWidth + buttonPadding, Height - 28.0f);

    _removeButton.Size = new Vector2(buttonWidth, 28.0f);
    _removeButton.Position = new Vector2(buttonWidth * 2.0f + buttonPadding * 2.0f, Height - 28.0f);
  }

  public ListConfigDisplayMode DisplayMode
  {
    get;
    set
    {
      field = value;
      _addButton.IsVisible = value.HasFlag(ListConfigDisplayMode.Add);
      _editButton.IsVisible = value.HasFlag(ListConfigDisplayMode.Edit);
      _removeButton.IsVisible = value.HasFlag(ListConfigDisplayMode.Remove);
    }
  } = ListConfigDisplayMode.Add | ListConfigDisplayMode.Edit | ListConfigDisplayMode.Remove;

  public List<T> Options
  {
    get;
    set
    {
      field = value;
      _listNode.OptionsList = value;
    }
  } = [];

  public List<Enum>? SortOptions
  {
    get => _searchWidget.SortingOptions;
    set
    {
      _searchWidget.SortingOptions = value ?? [];
      OnSizeChanged();

      if (value is not null && value.Count > 0)
      {
        OnSortOrderChanged(value.First(), false);
      }
    }
  }

  public Action<T?>? SelectionChanged { get; init; }

  public System.Action? AddNewEntry
  {
    get;
    set
    {
      field = value;
      UpdateButtonStates();
    }
  }

  public Action<T>? RemoveEntry
  {
    get;
    set
    {
      field = value;
      UpdateButtonStates();
    }
  }

  public Action<T>? EditEntry
  {
    get;
    set
    {
      field = value;
      UpdateButtonStates();
    }
  }

  public delegate int ItemCompareDelegate(T left, T right, Enum sortingMode);

  public ItemCompareDelegate? ItemComparer { get; set; }

  public delegate bool IsSearchMatchDelegate(T obj, string searchString);

  public IsSearchMatchDelegate? IsSearchMatch { get; set; }

  public T? SelectedOption { get; private set; }

  public float ItemSpacing
  {
    get => _listNode.ItemSpacing;
    set
    {
      _listNode.ItemSpacing = value;
      OnSizeChanged();
    }
  }

  private void RebuildList(Enum? sortingOption = null, bool? reversed = null, string? searchString = null)
  {
    List<T> baseList = Options ?? [];
    IEnumerable<T> result = baseList;

    string search = searchString ?? _searchWidget.SearchText;
    if (!string.IsNullOrEmpty(search) && IsSearchMatch is not null)
    {
      result = result.Where(item => IsSearchMatch(item, search));
    }

    Enum sortMode = sortingOption ?? _searchWidget.SortMode;
    bool isReversed = reversed ?? _searchWidget.IsReversed;

    if (ItemComparer is not null)
    {
      List<T> listCopy = result.ToList();
      listCopy.Sort((left, right) => ItemComparer.Invoke(left, right, sortMode) * (isReversed ? -1 : 1));
      _listNode.OptionsList = listCopy;
    }
    else
    {
      _listNode.OptionsList = result.ToList();
    }

    UpdateButtonStates();
  }

  private void OnSortOrderChanged(Enum sortingOption, bool reversed)
  {
    RebuildList(sortingOption, reversed, null);
  }

  private void OnSearchUpdated(string searchString)
  {
    RebuildList(null, null, searchString);
  }

  private void OnListItemSelected(T obj)
  {
    SelectedOption = obj;
    SelectionChanged?.Invoke(SelectedOption);

    UpdateButtonStates();
  }

  private void OnAddClicked()
  {
    AddNewEntry?.Invoke();
    RefreshList();
  }

  private void OnEditClicked()
  {
    if (SelectedOption is null) return;

    EditEntry?.Invoke(SelectedOption.Value);
    RefreshList();
  }

  private void OnRemoveClicked()
  {
    if (SelectedOption is null) return;

    RemoveEntry?.Invoke(SelectedOption.Value);
    RefreshList();
  }

  private void UpdateButtonStates()
  {
    _editButton.IsEnabled = SelectedOption is not null && EditEntry is not null;
    _removeButton.IsEnabled = SelectedOption is not null && RemoveEntry is not null;
    _addButton.IsEnabled = AddNewEntry is not null;
  }

  /// <summary>
  /// Refreshes the displayed list data.
  /// This resets scroll position, so don't spam it.
  /// </summary>
  public void RefreshList()
  {
    SelectedOption = null;
    RebuildList();
    _listNode.FullRebuild();
  }
}
