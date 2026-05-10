using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Premade.Node.Simple;

namespace XivVoices.Windows;

public class ConfigAddon : NativeAddon
{
  public ConfigTab CurrentTab = ConfigTab.Overview;

  private readonly IServiceProvider _services;
  private readonly Configuration _configuration;
  private readonly IDataService _dataService;

  private int _visibleTabButtons = 0;
  private readonly List<TabButtonNode> _tabButtons = [];
  private readonly List<TabPanelNode> _tabPanels = [];
  private TabButtonNode _patreonButtonNode = null!;
  private TabButtonNode _discordButtonNode = null!;
  private SimpleNineGridNode _verticalSeparator = null!;

  public ConfigAddon(Configuration configuration, IDataService dataService, IServiceProvider services)
  {
    _services = services;
    _configuration = configuration;
    _dataService = dataService;

    InternalName = "XivVoicesConfiguration";
    Title = "XivVoices Configuration";
    Size = new(450.0f, 600.0f);
    RespectCloseAll = false;
    Subtitle = "";
  }

  protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
  {
    _configuration.Saved += ConfigurationSaved;

    foreach (ConfigTab tab in Enum.GetValues<ConfigTab>())
    {
      (uint iconId, string tooltip, bool visible) = GetTabDetail(tab);
      TabButtonNode node = new()
      {
        IconId = iconId,
        Size = new Vector2(44.0f, 44.0f),
        StartPosition = new Vector2(ContentStartPosition.X, ContentStartPosition.Y),
        TextTooltip = tooltip,
        Index = _visibleTabButtons,
        Tab = tab,
        IsActive = CurrentTab == tab,
        // IsVisible = visible,
        IsEnabled = visible,
        OnClick = () => SetTab(tab),
      };
      // if (visible) _visibleTabButtons++;
      _visibleTabButtons++;
      node.AttachNode(this);
      _tabButtons.Add(node);
    }

    _patreonButtonNode = new()
    {
      IconId = 54,
      Size = new Vector2(44.0f, 44.0f),
      StartPosition = new Vector2(ContentStartPosition.X, ContentSize.Y - ContentPadding.X - (48.0f * 1)),
      Index = 0,
      TextTooltip = "Support us on Patreon!",
      OnClick = () => Util.OpenLink("https://xivv.keifufu.dev/patreon"),
    };
    _patreonButtonNode.AttachNode(this);

    _discordButtonNode = new()
    {
      IconId = 83,
      Size = new Vector2(44.0f, 44.0f),
      StartPosition = new Vector2(ContentStartPosition.X, ContentSize.Y - ContentPadding.X - (48.0f * 0)),
      Index = 0,
      TextTooltip = "Join us on Discord!",
      OnClick = () => Util.OpenLink("https://xivv.keifufu.dev/discord"),
    };
    _discordButtonNode.AddEvent(AtkEventType.MouseClick, OnDiscordButtonMouseClick);
    _discordButtonNode.AttachNode(this);

    _verticalSeparator = new SimpleNineGridNode
    {
      TexturePath = "ui/uld/ConfigSystem_hr1.tex",
      TextureCoordinates = new Vector2(56.0f, 0.0f),
      TextureSize = new Vector2(8.0f, 28.0f),
      TopOffset = 8.0f,
      BottomOffset = 8.0f,
      Position = new Vector2(60.0f, 34.0f),
      Height = Size.Y - 46.0f,
      Width = 8.0f,
    };
    _verticalSeparator.AttachNode(this);

    AddTabPanel<OverviewTabPanelNode>();
    AddTabPanel<DialogueSettingsTabPanelNode>();
    AddTabPanel<PlaybackSettingsTabPanelNode>();
    AddTabPanel<OverlaySettingsTabPanelNode>();
    AddTabPanel<AudioLogsTabPanelNode>();
    AddTabPanel<WineSettingsTabPanelNode>();
    AddTabPanel<DebugTabPanelNode>();
    AddTabPanel<SelfTestTabPanelNode>();

    SetTab(CurrentTab);
    ConfigurationSaved();
  }

  protected override unsafe void OnFinalize(AtkUnitBase* addon)
  {
    base.OnFinalize(addon);
    _configuration.Saved -= ConfigurationSaved;
    _visibleTabButtons = 0;
    _tabButtons.Clear();
    _tabPanels.Clear();
  }

  private void ConfigurationSaved()
  {
    _visibleTabButtons = 0;
    foreach (TabButtonNode node in _tabButtons)
    {
      // node.IsVisible = GetTabDetail(node.Tab).visible;
      node.IsEnabled = GetTabDetail(node.Tab).visible;
      node.Index = _visibleTabButtons;
      // if (node.IsVisible) _visibleTabButtons++;
      _visibleTabButtons++;
    }

    foreach (TabPanelNode node in _tabPanels)
    {
      if (node.IsActive) node.ConfigurationSaved();
    }

    Size = new Vector2(Size.X, 110 + ((_visibleTabButtons + 2) * 48.0f));
    if (WindowNode?.Size != Size)
    {
      WindowNode?.Size = Size;
      OnSizeChanged();
    }
  }

  private void OnSizeChanged()
  {
    _verticalSeparator.Height = Size.Y - 46;
    _patreonButtonNode.StartPosition = new Vector2(ContentStartPosition.X, ContentSize.Y - ContentPadding.X - (48.0f * 1));
    _patreonButtonNode.Index = 0;
    _discordButtonNode.StartPosition = new Vector2(ContentStartPosition.X, ContentSize.Y - ContentPadding.X - (48.0f * 0));
    _discordButtonNode.Index = 0;

    foreach (TabPanelNode node in _tabPanels)
    {
      node.Size = new Vector2(ContentSize.X - 60.0f, ContentSize.Y);
    }
  }

  private string? _lastDataDirectory = "";
  protected override unsafe void OnUpdate(AtkUnitBase* addon)
  {
    base.OnUpdate(addon);

    foreach (TabPanelNode node in _tabPanels)
    {
      if (node.IsActive) node.OnUpdate();
    }

    if (_dataService.DataDirectory != _lastDataDirectory)
    {
      _lastDataDirectory = _dataService.DataDirectory;
      ConfigurationSaved();
    }
  }

  private void AddTabPanel<T>() where T : TabPanelNode
  {
    T node = (T)Activator.CreateInstance(typeof(T), _services)!;
    node.Size = new Vector2(ContentSize.X - 60.0f, ContentSize.Y);
    node.Position = new Vector2(72.0f, ContentStartPosition.Y);
    node.AttachNode(this);
    _tabPanels.Add(node);
  }

  private (uint iconId, string tooltip, bool visible) GetTabDetail(ConfigTab tab)
  {
    bool disabled = _dataService.DataDirectory == null;
    return tab switch
    {
      ConfigTab.Overview => (1, "Overview", true),
      ConfigTab.DialogueSettings => (29, "Dialogue Settings", !disabled),
      ConfigTab.PlaybackSettings => (36, "Playback Settings", !disabled),
      ConfigTab.OverlaySettings => (42, "Overlay Settings", !disabled),
      ConfigTab.AudioLogs => (45, "Audio Logs", !disabled),
      ConfigTab.WineSettings => (35, "Wine Settings", !disabled && Util.IsWine()),
      ConfigTab.Debug => (28, "Debug", !disabled && _configuration.DebugMode),
      ConfigTab.SelfTest => (73, "Self-Test", !disabled && _configuration.DebugMode),
      _ => (0, "", false),
    };
  }

  public void SetTab(ConfigTab tab)
  {
    CurrentTab = tab;
    foreach (TabButtonNode node in _tabButtons)
      node.IsActive = node.Tab == tab;

    foreach (TabPanelNode node in _tabPanels)
      node.IsActive = node.Tab == tab;
  }

  private int _debugModeClickCount = 0;
  private DateTime _debugModeLastClickTime;
  private readonly TimeSpan _debugModeMaxClickInteral = TimeSpan.FromSeconds(1);
  private unsafe void OnDiscordButtonMouseClick(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
  {
    if (atkEventData->MouseData.ButtonId == 0)
    {
      Util.OpenLink("https://xivv.keifufu.dev/discord");
      return;
    }

    if (atkEventData->MouseData.ButtonId != 1) return;

    DateTime currentTime = DateTime.UtcNow;
    if (currentTime - _debugModeLastClickTime <= _debugModeMaxClickInteral)
      _debugModeClickCount++;
    else
      _debugModeClickCount = 1;

    _debugModeLastClickTime = currentTime;

    if (_debugModeClickCount >= 3)
    {
      _debugModeClickCount = 0;
      _configuration.DebugMode = !_configuration.DebugMode;
      _configuration.Save();

      if (CurrentTab == ConfigTab.Debug || CurrentTab == ConfigTab.SelfTest)
        SetTab(ConfigTab.Overview);
    }
  }

  public unsafe bool CheckCollision(AtkEventData* atkEventData)
  {
    return WindowNode?.CheckCollision(atkEventData) ?? false;
  }
}
