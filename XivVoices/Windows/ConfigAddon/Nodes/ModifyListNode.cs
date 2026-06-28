using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace XivVoices.Windows;

public class ModifyListNode<T, TU> : ResNode where T : struct where TU : ListItemNode<T>, IListItemNode, new()
{
  private string _searchText = string.Empty;
  private readonly IKeyState _keyState;

  public readonly TextInputNode _searchInputNode;
  private readonly CircleButtonNode _exportButton;
  private readonly ListNode<T, TU> _listNode;

  private readonly TextButtonNode _addButton;
  private readonly TextButtonNode _editButton;
  private readonly TextButtonNode _removeButton;

  public ModifyListNode(IKeyState keyState)
  {
    _keyState = keyState;
    _searchInputNode = new TextInputNode
    {
      PlaceholderString = "Search ...",
      String = _searchText,
      OnInputReceived = SearchTextChanged,
    };
    _searchInputNode.AttachNode(this);

    _exportButton = new CircleButtonNode()
    {
      Size = new Vector2(30.0f, 30.0f),
      OnClick = () =>
      {
        if (_exportButton?.Icon == CircleButtonIcon.CheckedBox || _exportButton?.Icon == CircleButtonIcon.Cross) return;

        (string result, bool success) result;
        if (_exportButton?.Icon == CircleButtonIcon.Undo)
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
    CircleButtonIcon importExportResultIcon = _lastImportExportSuccess ? CircleButtonIcon.CheckedBox : CircleButtonIcon.Cross;
    if (showImportExportResult && _lastImportExportSuccess && _lastWasImport && _exportButton.Icon != CircleButtonIcon.Undo)
    {
      _exportButton.Icon = CircleButtonIcon.Undo;
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
      _exportButton.Icon = CircleButtonIcon.Add;
      _exportButton.TextTooltip = "Import (Override duplicates)";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
    else if (!showImportExportResult && _keyState[VirtualKey.SHIFT] && !_keyState[VirtualKey.CONTROL] && !_exportButton.TextTooltip.ToString().Contains("(Hold Control"))
    {
      _exportButton.Icon = CircleButtonIcon.Add;
      _exportButton.TextTooltip = "Import (Hold Control to override duplicates)";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
    else if (!showImportExportResult && !_keyState[VirtualKey.SHIFT] && !_keyState[VirtualKey.CONTROL] && _exportButton.Icon != CircleButtonIcon.Document)
    {
      _exportButton.Icon = CircleButtonIcon.Document;
      _exportButton.TextTooltip = "Export (Hold Shift to Import)";
      if (tooltipVisible) _exportButton.ShowTooltip();
    }
  }

  private void SearchTextChanged(ReadOnlySeString newSearchString)
  {
    _searchText = newSearchString.ToString();
    RebuildList(_searchText);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _searchInputNode.Size = new Vector2(Width - _exportButton.Width - 14.0f, 28.0f);
    _searchInputNode.Position = new Vector2(5.0f, 5.0f);

    _exportButton.Position = new Vector2(_searchInputNode.Bounds.Right, 5.0f);

    _listNode.Size = new Vector2(Width, Height - _searchInputNode.Height - 40.0f);
    _listNode.Position = new Vector2(0.0f, _searchInputNode.Y + _searchInputNode.Height + 8.0f);

    const float buttonPadding = 5.0f;
    float buttonWidth = (Width - (buttonPadding * 2.0f)) / 3.0f;

    _addButton.Size = new Vector2(buttonWidth, 28.0f);
    _addButton.Position = new Vector2(0.0f, Height - 28.0f);

    _editButton.Size = new Vector2(buttonWidth, 28.0f);
    _editButton.Position = new Vector2(buttonWidth + buttonPadding, Height - 28.0f);

    _removeButton.Size = new Vector2(buttonWidth, 28.0f);
    _removeButton.Position = new Vector2(buttonWidth * 2.0f + buttonPadding * 2.0f, Height - 28.0f);
  }

  public List<T> Options
  {
    get;
    set
    {
      field = value;
      RefreshList();
    }
  } = [];

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

  public delegate int ItemCompareDelegate(T left, T right);

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

  private void RebuildList(string? searchString = null)
  {
    List<T> baseList = Options ?? [];
    IEnumerable<T> result = baseList;

    string search = searchString ?? _searchText;
    if (!string.IsNullOrEmpty(search) && IsSearchMatch is not null)
    {
      result = result.Where(item => IsSearchMatch(item, search));
    }

    if (ItemComparer is not null)
    {
      List<T> listCopy = result.ToList();
      listCopy.Sort(ItemComparer.Invoke);
      _listNode.OptionsList = listCopy;
    }
    else
    {
      _listNode.OptionsList = result.ToList();
    }

    UpdateButtonStates();
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

  public void RefreshList()
  {
    SelectedOption = null;
    RebuildList();
  }
}
