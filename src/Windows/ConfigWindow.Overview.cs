using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;

namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private readonly FileDialogManager _fileDialogManager = new();
  private string? _selectedPath = null;
  private string? _errorMessage = null;
  private bool _isImport = false;
  private string? _selectedDrive = null;

  // https://rgbcolorpicker.com/0-1
  private Vector4 _green = new(0.2f, 0.8f, 0.15f, 1.0f);
  private Vector4 _yellow = new(0.8f, 0.75f, 0.15f, 1.0f);
  private Vector4 _red = new(0.8f, 0.15f, 0.18f, 1.0f);
  private Vector4 _grey = new(0.25f, 0.25f, 0.25f, 1.0f);
  private Vector4 _lightgrey = new(0.729f, 0.729f, 0.729f, 1.0f);

  // Changelogs. Newest ones go to the top.
  private readonly Dictionary<string, string[]> _changelogs = new() {
    { "1.2.0.0", new[]
    {
      "Updated for 7.4/7.4HF1 (API14/NET10)",
      "Added the command '/xivv pause'.",
      "Added option to disable TTS for your own chat messages.",
      "Fixed controller support for temporarily pausing auto-advance.",
      "Fixed an issue where audio would stop playing after a long session.",
      "Fixed certain post-processed lines not being played.",
      "Removed playback history limit.",
      "Queued messages now clear when muting.",
    }},
    { "1.1.0.0", new[] {
      "Added option to prevent accidental dialogue advance.",
      "Added a fallback DirectSound audio device in case WaveOut fails.",
      "Added a warning in chat if the plugin is muted during login.",
      "Added an experimental option to use StreamElements for local TTS.",
      "Added a preliminary authentication flow to support upcoming backend features.",
      "Added a version indicator to the plugin window.",
      "Added the command '/xivv upload-logs'.",
      "Added the command '/xivv version'.",
      "Fixed ffmpeg-wine failing to start on some systems and exiting unexpectedly.",
      "Fixed own chat messages always using the default voice, regardless of gender.",
      "Fixed the updater showing an incorrect number of voicelines downloaded.",
      "Fixed concurrency issues with 'Queue Dialogue' enabled.",
      "Fixed directional chat messages becoming too quiet.",
    }},
    { "1.0.0.0", new[] {
      "Initial rewrite release!",
      "Starting from scratch, this plugin is way more maintainable and extendable than the old one, while maintaining feature parity.",
      "The new data architecture has fixed a lot of issues will make voice generation much simpler in the future.",
      "Improved error handling and logging has been added.",
      "The UI has been mostly revamped! It features a better setup/updater and more configurable options.",
      "Automatic reports now include more information to help us generate the voicelines.",
      "Manual reports are back! You can report any voicelines with a reason in /xivv logs.",
      "Debug options and self-tests have been added, but can you find how to enable them?",
      "And there's more to come!"
    }},
  };

  private void DrawHorizontallyCenteredText(string text, string? calcText = null)
  {
    float textWidth = ImGui.CalcTextSize(calcText ?? text).X;
    float textX = (ImGui.GetContentRegionAvail().X - textWidth) * 0.5f;
    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + textX);
    ImGui.TextWrapped(text);
  }

  private void DrawHorizontallyCenteredImage(ImTextureID textureId, Vector2 imageSize)
  {
    float imageWidth = imageSize.X;
    float imageX = (ImGui.GetContentRegionAvail().X - imageWidth) * 0.5f;
    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + imageX);
    ImGui.Image(textureId, imageSize);
  }

  private void DrawOverviewTab()
  {
    _fileDialogManager.Draw();

    IDalamudTextureWrap? logo = _textureProvider.GetFromFile(Path.Combine(_pluginInterface.AssemblyLocation.Directory?.FullName!, "logo.png")).GetWrapOrDefault();
    if (logo == null) return;
    DrawHorizontallyCenteredImage(logo.Handle, ScaledVector2(200, 200));

    DrawHorizontallyCenteredText("Welcome to XivVoices!");

    DrawHorizontallyCenteredText("Server Status:", $"Server Status: {_dataService.ServerStatus}");
    ImGui.SameLine();
    Vector4 serverStatusColor = _dataService.ServerStatus == ServerStatus.ONLINE ? _green : _red;
    using (ImRaii.PushColor(ImGuiCol.Text, serverStatusColor))
      ImGui.Text(_dataService.ServerStatus.ToString());

    DrawHorizontallyCenteredText("Voicelines:", $"Voicelines: {_dataService.DataStatus.VoicelinesDownloaded}");
    ImGui.SameLine();
    bool allVoicelinesDownloaded = _dataService.Manifest != null && _dataService.DataStatus.VoicelinesDownloaded == _dataService.Manifest.Voicelines.Count;
    bool mostVoicelinesDownloaded = _dataService.Manifest != null && (_dataService.DataStatus.VoicelinesDownloaded + 10000) >= _dataService.Manifest.Voicelines.Count;
    Vector4 voicelineColor = allVoicelinesDownloaded ? _green : mostVoicelinesDownloaded ? _yellow : _red;
    using (ImRaii.PushColor(ImGuiCol.Text, voicelineColor))
      ImGui.Text(_dataService.DataStatus.VoicelinesDownloaded.ToString());

    bool isUpToDate = _dataService.Version == _dataService.LatestVersion;
    DrawHorizontallyCenteredText("Version:", $"Version: {_dataService.Version}");
    ImGui.SameLine();
    Vector4 versionColor = isUpToDate ? _green : _red;
    using (ImRaii.PushColor(ImGuiCol.Text, versionColor))
      ImGui.Text(_dataService.Version);

    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text(isUpToDate ? "Your plugin is up to date." : $"Your plugin is outdated. Latest version available: {_dataService.LatestVersion}");

    ImGui.Dummy(ScaledVector2(0, 10));

    if (_dataService.ServerStatus == ServerStatus.UNAUTHORIZED)
    {
      using (ImRaii.PushColor(ImGuiCol.Button, _grey))
      {
        using (ImRaii.Disabled(_dataService.IsLoggingIn))
        {
          if (ImGui.Button(_dataService.IsLoggingIn ? "Awaiting Login" : "Login with Discord", new(ImGui.GetContentRegionAvail().X - ScaledFloat(8), ScaledFloat(40))))
          {
            _dataService.Login();
          }
        }
      }

      ImGui.Dummy(ScaledVector2(0, 10));
      using (ImRaii.PushColor(ImGuiCol.Text, _lightgrey))
        DrawHorizontallyCenteredText("Login is required to ensure our security standards are met.");
    }
    else if (_dataService.DataDirectory == null)
    {
      if (Util.IsWine())
      {
        ImGui.TextWrapped("Wine Detected! It is recommended you select an installation directory on Z: or X:, as C: is your wineprefix. Please manually select a folder your user has access to via \"Select Directory\".");
        ImGui.Dummy(ScaledVector2(0, 10));
      }

      ImGui.SetNextItemWidth(ScaledFloat(160));
      using (ImRaii.IEndObject combo = ImRaii.Combo("##Drives", _selectedDrive ?? "Select Drive"))
      {
        if (combo)
        {
          for (int i = 0; i < _dataService.AvailableDrives.Count; i++)
          {
            if (ImGui.Selectable(_dataService.AvailableDrives[i]))
            {
              _selectedDrive = _dataService.AvailableDrives[i];
              _selectedPath = Path.Join(_selectedDrive, "XivVoices").Replace("\\", "/");
              _isImport = false;
              _errorMessage = null;
            }
          }
        }
      }

      ImGui.SameLine();
      ImGui.Text("OR");
      ImGui.SameLine();

      using (ImRaii.PushColor(ImGuiCol.Button, _grey))
      {
        if (ImGui.Button("Select Directory", ScaledVector2(160, 22)))
        {
          _fileDialogManager.OpenFolderDialog("Select installation directory", (ok, path) =>
          {
            if (!ok) return;
            path = path.Replace("\\", "/");

            if (File.Exists(Path.Join(path, "Data.json")))
            {
              _errorMessage = "The installation you selected is incompatible.\nPlease create a new one.";
              _selectedPath = null;
              _isImport = false;
              _selectedDrive = null;
              return;
            }

            if (File.Exists(Path.Join(path, "manifest.json")) && File.Exists(Path.Join(path, "tools.md5")))
            {
              _errorMessage = null;
              _selectedPath = path;
              _isImport = true;
              _selectedDrive = null;
              return;
            }

            _errorMessage = null;
            _isImport = false;
            _selectedDrive = null;
            _selectedPath = Path.Join(path, "XivVoices").Replace("\\", "/");
          }, "C:");
        }
      }

      if (_selectedPath != null && !_isImport && _dataService.ServerStatus != ServerStatus.ONLINE)
      {
        _errorMessage = "You selected a new installation directory,\nbut the server is currently offline.\nPlease try again later or select an existing installation.";
        _selectedPath = null;
        _isImport = false;
        _selectedDrive = null;
      }

      ImGui.Dummy(ScaledVector2(0, 10));

      if (_errorMessage != null)
        using (ImRaii.PushColor(ImGuiCol.Text, _red))
          DrawHorizontallyCenteredText(_errorMessage);

      if (_selectedPath != null)
      {
        DrawHorizontallyCenteredText($"Installation Directory: {_selectedPath}");

        using (ImRaii.PushColor(ImGuiCol.Button, _grey))
        {
          if (ImGui.Button(_isImport ? "Import" : "Install", ScaledVector2(350, 40)))
          {
            _dataService.SetDataDirectory(_selectedPath);
            _selectedPath = null;
          }
        }
      }
    }
    else
    {
      using (ImRaii.PushColor(ImGuiCol.Button, _grey))
      {
        if (ImGui.Button(_dataService.DataStatus.UpdateInProgress ? "Cancel Update" : "Check for Updates", new(ImGui.GetContentRegionAvail().X - ScaledFloat(8), ScaledFloat(40))))
        {
          if (_dataService.DataStatus.UpdateInProgress)
            _dataService.CancelUpdate();
          else
            _dataService.Update();
        }
      }

      if (_dataService.DataStatus.UpdateInProgress)
      {
        TimeSpan ETA = _dataService.DataStatus.UpdateETA;
        ImGui.Dummy(ScaledVector2(0, 10));
        ImGui.ProgressBar(_dataService.DataStatus.UpdateProgressPercent, new Vector2(ImGui.GetContentRegionAvail().X - ScaledFloat(8), ScaledFloat(24)), "ETA: " + (ETA == TimeSpan.MaxValue ? "Calculating..." : ETA.ToString(@"hh\:mm\:ss")));
      }
    }

    ImGui.Unindent(ScaledFloat(8));
    ImGui.SetCursorPosY(ImGui.GetContentRegionAvail().Y + ImGui.GetCursorPosY() - ScaledFloat(150));
    using (ImRaii.IEndObject child = ImRaii.Child("##changelogs", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true, ImGuiWindowFlags.AlwaysVerticalScrollbar))
    {
      if (!child.Success) return;

      bool first = true;
      foreach ((string version, string[] notes) in _changelogs)
      {
        using (ImRaii.PushId(version))
        {
          ImGuiTreeNodeFlags flags = first ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
          if (ImGui.CollapsingHeader($"Version {version}", flags))
          {
            foreach (string note in notes)
            {
              ImGui.Bullet();
              ImGui.TextWrapped(note);
            }
          }
        }
        first = false;
      }
    }
  }
}
