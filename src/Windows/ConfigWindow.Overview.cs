using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;

namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private readonly FileDialogManager _fileDialogManager = new();
  private string? _selectedPath = null;
  private string? _errorMessage = null;

  // https://rgbcolorpicker.com/0-1
  private Vector4 _green = new(0.2f, 0.8f, 0.15f, 1.0f);
  private Vector4 _yellow = new(0.8f, 0.75f, 0.15f, 1.0f);
  private Vector4 _red = new(0.8f, 0.15f, 0.18f, 1.0f);
  private Vector4 _grey = new(0.25f, 0.25f, 0.25f, 1.0f);
  private Vector4 _lightgrey = new(0.729f, 0.729f, 0.729f, 1.0f);

  // Changelogs. Newest ones go to the top.
  private readonly Dictionary<string, string[]> _changelogs = new() {
    { "1.0.0.0", [ "Initial rewrite release" ] },
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

    DrawHorizontallyCenteredText("Server Status:", "Server Status: " + (_dataService.ServerOnline ? "ONLINE" : "OFFLINE"));
    ImGui.SameLine();
    Vector4 serverStatusColor = _dataService.ServerOnline ? _green : _red;
    using (ImRaii.PushColor(ImGuiCol.Text, serverStatusColor))
      ImGui.Text(_dataService.ServerOnline ? "ONLINE" : "OFFLINE");

    DrawHorizontallyCenteredText("Voicelines:", $"Voicelines: {_dataService.DataStatus.VoicelinesDownloaded}");
    ImGui.SameLine();
    bool allVoicelinesDownloaded = _dataService.Manifest != null && _dataService.DataStatus.VoicelinesDownloaded == _dataService.Manifest.Voicelines.Count;
    bool mostVoicelinesDownloaded = _dataService.Manifest != null && (_dataService.DataStatus.VoicelinesDownloaded + 10000) >= _dataService.Manifest.Voicelines.Count;
    Vector4 voicelineColor = allVoicelinesDownloaded ? _green : mostVoicelinesDownloaded ? _yellow : _red;
    using (ImRaii.PushColor(ImGuiCol.Text, voicelineColor))
      ImGui.Text(_dataService.DataStatus.VoicelinesDownloaded.ToString());

    ImGui.Dummy(ScaledVector2(0, 10));

    if (_dataService.DataDirectory == null)
    {
      using (ImRaii.PushColor(ImGuiCol.Button, _grey))
      {
        if (ImGui.Button("Select Installation Directory", ScaledVector2(350, 40)))
        {
          _fileDialogManager.SaveFolderDialog("Select installation directory", "XivVoices", (ok, path) =>
          {
            if (!ok) return;
            path = path.Replace("\\", "/");
            if (Directory.EnumerateFileSystemEntries(path).Any())
            {
              string legacyPath = Path.Join(path, "Data.json");
              if (File.Exists(legacyPath))
              {
                _errorMessage = "The installation you selected is incompatible.\nPlease create a new one.";
                _selectedPath = null;
                return;
              }

              string manifestPath = Path.Join(path, "manifest.json");
              if (!File.Exists(manifestPath))
              {
                _errorMessage = "The folder you selected is not empty\nand not a valid XivVoices installation.";
                _selectedPath = null;
                return;
              }

              _errorMessage = null;
              _selectedPath = path;
              return;
            }
            else if (!_dataService.ServerOnline)
            {
              _errorMessage = "You selected a new installation directory,\nbut the server is currently offline.\nPlease try again later or select\nan existing installation.";
              _selectedPath = null;
              return;
            }

            _errorMessage = null;
            _selectedPath = path;
          });
        }
      }

      ImGui.Dummy(ScaledVector2(0, 10));

      if (_errorMessage != null)
        using (ImRaii.PushColor(ImGuiCol.Text, _red))
          DrawHorizontallyCenteredText(_errorMessage);

      if (_selectedPath != null)
      {
        DrawHorizontallyCenteredText($"Selected Path: {_selectedPath}");

        using (ImRaii.PushColor(ImGuiCol.Button, _grey))
        {
          if (ImGui.Button("Install / Import", ScaledVector2(350, 40)))
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
            _dataService.Update(true);
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
