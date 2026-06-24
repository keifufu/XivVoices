using System.ComponentModel;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Lumina.Text.ReadOnly;

// Just SearchWidget, which was removed with #111
namespace KamiToolKit.Premade.Node;

public class SearchWidget : SimpleComponentNode
{
  public readonly TextInputNode InputNode;
  public readonly EnumDropDownNode<Enum> SortOrderDropDown;
  public readonly CircleButtonNode ReverseButtonNode;

  public bool IsReversed { get; private set; }
  public string SearchText { get; private set; } = string.Empty;
  public Enum SortMode { get; private set; } = DefaultSortOptions.Unset;

  public delegate void SearchUpdated(string searchString);

  public delegate void SortUpdated(Enum sortingMode, bool reversed);

  public SearchWidget()
  {
    InputNode = new TextInputNode
    {
      PlaceholderString = "Search . . .",
      String = SearchText,
      OnInputReceived = SearchTextChanged,
    };
    InputNode.AttachNode(this);

    SortOrderDropDown = new EnumDropDownNode<Enum>
    {
      MaxListOptions = 0,
      Options = [],
      IsVisible = false,
      SelectedOption = (DefaultSortOptions)SortMode == DefaultSortOptions.Unset ? DefaultSortOptions.Alphabetical : SortMode,
      OnOptionSelected = DropDownChanged,
    };
    SortOrderDropDown.AttachNode(this);

    ReverseButtonNode = new CircleButtonNode
    {
      Icon = CircleButtonIcon.Sort,
      OnClick = OnReverseButtonClicked,
      TextTooltip = "Reverse Sort Direction",
      IsVisible = false,
    };
    ReverseButtonNode.AttachNode(this);

    // ResNode->SetHeight(38);
  }

  public required SortUpdated OnSortOrderChanged { get; set; }

  private void OnReverseButtonClicked()
  {
    IsReversed = !IsReversed;
    OnSortOrderChanged(SortMode, IsReversed);
  }

  private void DropDownChanged(Enum newOption)
  {
    SortMode = newOption;
    OnSortOrderChanged(SortMode, IsReversed);
  }

  public required SearchUpdated OnSearchUpdated { get; set; }

  private void SearchTextChanged(ReadOnlySeString newSearchString)
  {
    SearchText = newSearchString.ToString();
    OnSearchUpdated(SearchText);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    InputNode.Size = new Vector2(Width - 10.0f, 28.0f);
    InputNode.Position = new Vector2(5.0f, 5.0f);

    ReverseButtonNode.Size = new Vector2(28.0f, 28.0f);
    ReverseButtonNode.Position = new Vector2(Width - 5.0f - ReverseButtonNode.Width, InputNode.Height + 8.0f);

    SortOrderDropDown.Size = new Vector2(Width - 5.0f - ReverseButtonNode.Width - 5.0f - 5.0f, 28.0f);
    SortOrderDropDown.Position = new Vector2(5.0f, InputNode.Height + 8.0f);
  }

  // Disallow modifying the height of this element.
  public override float Height { get => base.Height; set { } }

  public int MaxDropdownOptions
  {
    get => SortOrderDropDown.MaxListOptions;
    set => SortOrderDropDown.MaxListOptions = value;
  }

  public List<Enum> SortingOptions
  {
    get => SortOrderDropDown.Options;
    set
    {
      SortOrderDropDown.Options = value;
      SortOrderDropDown.MaxListOptions = value.Count / 2 + 1;
      SortOrderDropDown.IsVisible = value.Count > 0;
      ReverseButtonNode.IsVisible = value.Count > 0;

      // ResNode->SetHeight((ushort)(value.Count > 0 ? 69 : 38));

      if (SortingOptions.Count > 0)
      {
        SortMode = value.First();
      }
    }
  }

  public Enum? SelectedOption => SortOrderDropDown.SelectedOption;
}

public enum DefaultSortOptions
{
  [Description("ERROR: Unset")]
  Unset,
  [Description("Alphabetical")]
  Alphabetical,
  [Description("Id")]
  Id,
}
