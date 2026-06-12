using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace XivVoices.Windows;

public class OverviewTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.Overview;
  private IDalamudPluginInterface _pluginInterface = null!;
  private IDataService _dataService = null!;
  private IKeyState _keyState = null!;
  private ILogger _logger = null!;

  private ImGuiImageNode _logoNode = null!;
  private TextNode _welcomeNode = null!;
  private TextNode _serverStatusNode = null!;
  private TextNode _voicelinesNode = null!;
  private TextNode _versionNode = null!;
  private TextButtonNode _actionButtonNode = null!;
  private TextNode _loginNoteNode = null!;
  private TextDropDownNode _selectDriveNode = null!;
  private TextNode _orNode = null!;
  private TextButtonNode _selectDirectoryNode = null!;
  private TextNode _selectedPathNode = null!;
  private TextNode _changelogHeaderNode = null!;
  private ScrollingTreeNode _changelogNode = null!;

  private string? _selectedPath = null;
  private bool _isImport = false;

  public override void OnSetup()
  {
    _pluginInterface = _services.GetRequiredService<IDalamudPluginInterface>();
    _dataService = _services.GetRequiredService<IDataService>();
    _keyState = _services.GetRequiredService<IKeyState>();
    _logger = _services.GetRequiredService<ILogger>();

    _dataService.OnDataDirectoryChanged += OnDataDirectoryChanged;
    _dataService.OnLatestVersionChanged += OnLatestVersionChanged;
    _dataService.OnServerStatusChanged += OnServerStatusChanged;
    _dataService.OnUpdateFinished += OnUpdateFinished;

    _logoNode = new ImGuiImageNode()
    {
      TexturePath = Path.Combine(_pluginInterface.AssemblyLocation.Directory?.FullName!, "logo.png"),
      Size = new Vector2(200.0f),
      FitTexture = true,
    };
    AttachNode(_logoNode);

    _welcomeNode = new()
    {
      String = "Welcome to XivVoices!",
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
    };
    AttachNode(_welcomeNode);

    _serverStatusNode = new()
    {
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
    };
    AttachNode(_serverStatusNode);

    _voicelinesNode = new()
    {
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
    };
    AttachNode(_voicelinesNode);

    _versionNode = new()
    {
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
    };
    AttachNode(_versionNode);

    _actionButtonNode = new()
    {
      Size = new Vector2(300.0f, 40.0f),
      OnClick = () =>
      {
        if (_dataService.ServerStatus == ServerStatus.UNAUTHORIZED)
        {
          _dataService.Login();
        }
        else if (_dataService.DataDirectory == null)
        {
          if (_selectedPath != null)
          {
            _dataService.SetDataDirectory(_selectedPath);
            _selectedPath = null;
          }
        }
        else
        {
          if (_keyState[VirtualKey.CONTROL] && _keyState[VirtualKey.SHIFT])
            _dataService.CancelUpdate();
          else
            _dataService.Update(true);
        }

        UpdateLoginInstallUpdateState();
      }
    };
    AttachNode(_actionButtonNode);

    _selectDriveNode = new()
    {
      Options = ["Select Drive", .. _dataService.AvailableDrives],
      Size = new Vector2(130.0f, 28.0f),
      OnOptionSelected = (option) =>
      {
        if (option == "Select Drive")
        {
          _selectedPath = null;
          _isImport = false;
        }
        else
        {
          _selectedPath = Path.Join(option, "XivVoices").Replace("\\", "/");
          _isImport = IsImport(_selectedPath);
        }
        UpdateLoginInstallUpdateState();
      }
    };
    AttachNode(_selectDriveNode);

    _orNode = new()
    {
      String = "OR",
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
      IsVisible = false,
    };
    AttachNode(_orNode);

    _selectDirectoryNode = new()
    {
      String = "Select Directory",
      Size = new Vector2(150.0f, 32.0f),
      OnClick = () =>
      {
        _dataService.FileDialogManager.OpenFolderDialog("Select installation directory", (ok, path) =>
        {
          if (!ok) return;
          path = path.Replace("\\", "/");
          _selectDriveNode.SelectedOption = "Select Drive";

          // Old incompatible XivVoices installation
          if (File.Exists(Path.Join(path, "Data.json")))
          {
            _selectedPath = null;
            _isImport = false;
            _logger.DalamudToast(NotificationType.Error, "Invalid Path", "The installation you selected is incompatible, please create a new one.", 15);
            UpdateLoginInstallUpdateState();
            return;
          }

          // Existing and new XivVoices installation
          if (IsImport(path))
          {
            _selectedPath = path;
            _isImport = true;
            UpdateLoginInstallUpdateState();
            return;
          }

          _selectedPath = Path.Join(path, "XivVoices").Replace("\\", "/");
          _isImport = false;
          UpdateLoginInstallUpdateState();
        }, Util.IsWine() ? "Z:" : "C:");
      }
    };
    AttachNode(_selectDirectoryNode);

    _loginNoteNode = new()
    {
      String = "Login is required to ensure our security standards are met.",
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
      IsVisible = false,
    };
    AttachNode(_loginNoteNode);

    _selectedPathNode = new()
    {
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
      IsVisible = false,
    };
    AttachNode(_selectedPathNode);

    _changelogHeaderNode = new()
    {
      String = "Plugin Changelogs. Join our Discord for announcements.",
      Size = new Vector2(200.0f, 20.0f),
      TextFlags = TextFlags.AutoAdjustNodeSize,
      IsVisible = false,
    };
    AttachNode(_changelogHeaderNode);

    _changelogNode = new()
    {
      CategoryVerticalSpacing = 0.0f,
    };
    AttachNode(_changelogNode);

    foreach (KeyValuePair<string, string[]> version in Changelog.Versions)
    {
      TreeListCategoryNode categoryNode = new()
      {
        String = version.Key,
        OnToggle = _ => _changelogNode.RecalculateLayout(),
      };

      categoryNode.AddNode(new ResNode() { Height = 2.0f });
      foreach (string changelog in version.Value)
      {
        IEnumerable<string> sections = Wrap(changelog);
        for (int i = 0; i < sections.Count(); i++)
        {
          string section = sections.ElementAt(i);
          TextNode textNode = new()
          {
            String = i == 0 ? " " + section : section,
            Width = _changelogNode.TreeListNode.Width,
            Height = 12.0f,
            X = i == 0 ? 18.0f : 33.0f,
          };
          categoryNode.AddNode(textNode);
        }

        categoryNode.AddNode(new ResNode() { Height = 2.0f });
      }

      _changelogNode.AddCategoryNode(categoryNode);
    }
  }

  private static IEnumerable<string> Wrap(string? s, int max = 44)
  {
    if (string.IsNullOrWhiteSpace(s) || max <= 0) yield break;
    StringBuilder cur = new();
    foreach (string word in s.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries))
    {
      if (word.Length > max)
      {
        if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); }
        for (int i = 0; i < word.Length; i += max)
          yield return word.Substring(i, Math.Min(max, word.Length - i));
        continue;
      }

      if (cur.Length == 0) cur.Append(word);
      else if (cur.Length + 1 + word.Length <= max) cur.Append(' ').Append(word);
      else { yield return cur.ToString(); cur.Clear(); cur.Append(word); }
    }

    if (cur.Length > 0) yield return cur.ToString();
  }

  private bool IsImport(string path)
  {
    return File.Exists(Path.Join(path, "manifest.json")) && File.Exists(Path.Join(path, "tools.md5"));
  }

  protected override void Dispose(bool disposing, bool isNativeDestructor)
  {
    base.Dispose(disposing, isNativeDestructor);

    _dataService.OnDataDirectoryChanged -= OnDataDirectoryChanged;
    _dataService.OnLatestVersionChanged -= OnLatestVersionChanged;
    _dataService.OnServerStatusChanged -= OnServerStatusChanged;
    _dataService.OnUpdateFinished -= OnUpdateFinished;
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    _logoNode.X = (Width - _logoNode.Size.X) / 2.0f;
    _welcomeNode.Position = new Vector2((Width - _welcomeNode.Size.X) / 2.0f, _logoNode.Bounds.Bottom + 8.0f);

    OnDataDirectoryChanged();
    OnServerStatusChanged();
    OnVoicelinesChanged();
    OnLatestVersionChanged();

    _actionButtonNode.Position = new Vector2((Width - _actionButtonNode.Size.X) / 2.0f, _versionNode.Bounds.Bottom + 8.0f);
    _loginNoteNode.Position = new Vector2((Width - _loginNoteNode.Size.X) / 2.0f, _actionButtonNode.Bounds.Bottom + 8.0f);
    _selectDriveNode.Position = new Vector2(_actionButtonNode.Bounds.Left, _actionButtonNode.Bounds.Bottom + 9.0f);
    _orNode.Position = new Vector2(_selectDriveNode.Bounds.Right, _actionButtonNode.Bounds.Bottom + 12.0f);
    _selectDirectoryNode.Position = new Vector2(_orNode.Bounds.Right + 4.0f, _actionButtonNode.Bounds.Bottom + 8.0f);
    _selectDirectoryNode.Size = new Vector2(_actionButtonNode.Bounds.Right - _selectDirectoryNode.Position.X, _selectDirectoryNode.Size.Y);
    _changelogHeaderNode.Position = new Vector2((Width - _changelogHeaderNode.Size.X) / 2.0f, _actionButtonNode.Bounds.Bottom + 18.0f);
    _changelogNode.Position = new Vector2(0, _changelogHeaderNode.Bounds.Bottom);
    _changelogNode.Size = new Vector2(Width, Height - _changelogNode.Y);
    _changelogNode.RecalculateLayout();
    for (int i = 0; i < _changelogNode.CategoryNodes.Count; i++)
    {
      TreeListCategoryNode node = _changelogNode.CategoryNodes[i];
      node.Width = _changelogNode.TreeListNode.Width;
      node.IsCollapsed = i != 0;
      node.RecalculateLayout();
    }
    _changelogNode.RecalculateLayout();
  }

  private void OnDataDirectoryChanged()
  {
    UpdateLoginInstallUpdateState();
  }

  private void OnServerStatusChanged()
  {
    if (!SetupComplete) return;

    using RentedSeStringBuilder builder = new();
    _serverStatusNode.String = builder.Builder.Append("Server Status: ").PushColorType(_dataService.ServerStatus == ServerStatus.ONLINE ? 45u : 15u).Append(_dataService.ServerStatus).GetViewAsSpan();
    _serverStatusNode.Position = new Vector2((Width - _serverStatusNode.Size.X) / 2.0f, _welcomeNode.Bounds.Bottom);

    UpdateLoginInstallUpdateState();
  }

  private void OnVoicelinesChanged()
  {
    if (!SetupComplete) return;

    using RentedSeStringBuilder builder = new();
    bool allVoicelinesDownloaded = _dataService.Manifest != null && _dataService.DataStatus.VoicelinesDownloaded == _dataService.Manifest.Voicelines.Count;
    bool mostVoicelinesDownloaded = _dataService.Manifest != null && (_dataService.DataStatus.VoicelinesDownloaded + 10000) >= _dataService.Manifest.Voicelines.Count;
    _voicelinesNode.String = builder.Builder.Append("Voicelines: ").PushColorType(allVoicelinesDownloaded ? 45u : mostVoicelinesDownloaded ? 25u : 15u).Append(_dataService.DataStatus.VoicelinesDownloaded).GetViewAsSpan();
    _voicelinesNode.Position = new Vector2((Width - _voicelinesNode.Size.X) / 2.0f, _serverStatusNode.Bounds.Bottom);
  }

  private void OnLatestVersionChanged()
  {
    if (!SetupComplete) return;

    using RentedSeStringBuilder builder = new();
    _versionNode.String = builder.Builder.Append("Version: ").PushColorType(_dataService.IsOutdated ? 15u : 45u).Append(_dataService.Version).GetViewAsSpan();
    _versionNode.Position = new Vector2((Width - _versionNode.Size.X) / 2.0f, _voicelinesNode.Bounds.Bottom);
    _versionNode.TextTooltip = _dataService.IsOutdated ? $"Your plugin is outdated. Latest version available: {_dataService.LatestVersion}" : "Your plugin is up to date.";
  }

  private void UpdateLoginInstallUpdateState()
  {
    if (!SetupComplete) return;

    _actionButtonNode.IsEnabled = true;
    _loginNoteNode.IsVisible = false;
    _selectDriveNode.IsVisible = false;
    _orNode.IsVisible = false;
    _selectDirectoryNode.IsVisible = false;
    _selectedPathNode.IsVisible = false;
    _changelogHeaderNode.IsVisible = false;
    _changelogNode.IsVisible = false;

    if (_dataService.ServerStatus == ServerStatus.UNAUTHORIZED)
    {
      if (_dataService.IsLoggingIn)
      {
        _actionButtonNode.IsEnabled = false;
        _actionButtonNode.String = "Awaiting Login...";
      }
      else
      {
        _actionButtonNode.String = "Login with Discord";
      }

      _loginNoteNode.IsVisible = true;
    }
    else if (_dataService.DataDirectory == null)
    {
      _selectDriveNode.IsVisible = true;
      _orNode.IsVisible = true;
      _selectDirectoryNode.IsVisible = true;
      _actionButtonNode.String = _isImport ? "Import" : "Install";
      _actionButtonNode.IsEnabled = _selectedPath != null;
      _selectedPathNode.String = $"Installation Directory: {_selectedPath}";
      _selectedPathNode.Position = new Vector2((Width - _selectedPathNode.Size.X) / 2.0f, _selectDirectoryNode.Bounds.Bottom + 8.0f);
      _selectedPathNode.IsVisible = _selectedPath != null;
    }
    else
    {
      _changelogHeaderNode.IsVisible = true;
      _changelogNode.IsVisible = true;
      if (_dataService.DataStatus.UpdateInProgress)
      {
        TimeSpan ETA = _dataService.DataStatus.UpdateETA;
        _actionButtonNode.String = "ETA: " + (ETA == TimeSpan.MaxValue ? "Calculating..." : ETA.ToString(@"hh\:mm\:ss"));
      }
      else
      {
        _actionButtonNode.String = "Check for Updates";
      }
    }
  }

  private void OnUpdateFinished()
  {
    OnVoicelinesChanged();
    UpdateLoginInstallUpdateState();
  }

  public override void OnUpdate()
  {
    if (!SetupComplete) return;
    _serverStatusNode.Position = new Vector2((Width - _serverStatusNode.Size.X) / 2.0f, _welcomeNode.Bounds.Bottom);
    _voicelinesNode.Position = new Vector2((Width - _voicelinesNode.Size.X) / 2.0f, _serverStatusNode.Bounds.Bottom);
    _versionNode.Position = new Vector2((Width - _versionNode.Size.X) / 2.0f, _voicelinesNode.Bounds.Bottom);

    if (_dataService.DataStatus.UpdateInProgress)
    {
      OnVoicelinesChanged();
      UpdateLoginInstallUpdateState();
    }
  }
}
