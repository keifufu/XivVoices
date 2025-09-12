using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;

namespace XivVoices.Windows;

public enum ConfigWindowTab
{
  Overview,
  DialogueSettings,
  AudioSettings,
  AudioLogs,
  WineSettings,
  Debug,
  SelfTest,
}

public partial class ConfigWindow(ILogger _logger, Configuration _configuration, IDataService _dataService, IReportService _reportService, IPlaybackService _playbackService, ISelfTestService _selfTestService, IAudioPostProcessor _audioPostProcessor, ITextureProvider _textureProvider, IDalamudPluginInterface _pluginInterface) : Window("XivVoices###XivVoicesConfigWindow")
{
  public ConfigWindowTab SelectedTab { get; set; } = ConfigWindowTab.Overview;

  private int _debugModeClickCount = 0;
  private double _debugModeLastClickTime;
  private readonly double _debugModeMaxClickInteral = 0.5;
  private bool _promptOpen = false;
  private string _promptDescription = "";
  private string _promptInputBuffer = "";
  private Action<string>? _promptCallback = null;

  private readonly IFontHandle _uiFont = _pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
  {
    e.OnPreBuild(tk =>
    {
      float fontPx = UiBuilder.DefaultFontSizePx;
      SafeFontConfig safeFontConfig = new() { SizePx = fontPx };
      tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansJpMedium, safeFontConfig);
      tk.AttachExtraGlyphsForDalamudLanguage(safeFontConfig);
    });
  });

  private float ScaledFloat(float value) => value * ImGuiHelpers.GlobalScale;
  private Vector2 ScaledVector2(float x, float? y = null) => new Vector2(x, y ?? x) * ImGuiHelpers.GlobalScale;

  public override void Draw()
  {
    using IDisposable _ = _uiFont.Push();

    Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;
    SizeCondition = ImGuiCond.Always;
    Size = new(440, 700);
    RespectCloseHotkey = _dataService.DataDirectory != null;

    Vector2 originPos = ImGui.GetCursorPos();
    ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMax().X + ScaledFloat(8));
    ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - ScaledFloat(26));
    DrawDiscordButton();
    ImGui.SetCursorPos(originPos);

    using (ImRaii.IEndObject child = ImRaii.Child("Sidebar", ScaledVector2(50, 500), false))
    {
      if (child.Success)
      {
        DrawImageButton(ConfigWindowTab.Overview, "Overview", GetImGuiHandleForIconId(1));
        DrawImageButton(ConfigWindowTab.DialogueSettings, "Dialogue Settings", GetImGuiHandleForIconId(29));
        DrawImageButton(ConfigWindowTab.AudioSettings, "Audio Settings", GetImGuiHandleForIconId(36));
        DrawImageButton(ConfigWindowTab.AudioLogs, "Audio Logs", GetImGuiHandleForIconId(45));
        if (Util.IsWine())
          DrawImageButton(ConfigWindowTab.WineSettings, "Wine Settings", GetImGuiHandleForIconId(24423));
        if (_configuration.DebugMode)
        {
          DrawImageButton(ConfigWindowTab.Debug, "Debug", GetImGuiHandleForIconId(28));
          DrawImageButton(ConfigWindowTab.SelfTest, "Self-Test", GetImGuiHandleForIconId(73));
        }
      }
    }

    ImGui.SameLine();
    ImDrawListPtr drawList = ImGui.GetWindowDrawList();
    Vector2 lineStart = ImGui.GetCursorScreenPos() - new Vector2(0, 10);
    Vector2 lineEnd = new(lineStart.X, lineStart.Y + ScaledFloat(700));
    uint lineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.15f, 1));
    drawList.AddLine(lineStart, lineEnd, lineColor, 1f);
    ImGui.SameLine(ScaledFloat(85));

    using (ImRaii.IEndObject group = ImRaii.Group())
    {
      if (!group.Success) return;
      ImGui.Unindent(ScaledFloat(8));
      switch (SelectedTab)
      {
        case ConfigWindowTab.Overview:
          DrawOverviewTab();
          break;
        case ConfigWindowTab.DialogueSettings:
          DrawDialogueSettingsTab();
          break;
        case ConfigWindowTab.AudioSettings:
          DrawAudioSettingsTab();
          break;
        case ConfigWindowTab.AudioLogs:
          DrawAudioLogsTab();
          break;
        case ConfigWindowTab.WineSettings:
          DrawWineTab();
          break;
        case ConfigWindowTab.Debug:
          DrawDebugTab();
          break;
        case ConfigWindowTab.SelfTest:
          DrawSelfTestTab();
          break;
      }
    }

    DrawInputPrompt();
  }

  private void DrawDiscordButton()
  {
    Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? discord = _textureProvider.GetFromFile(Path.Join(_pluginInterface.AssemblyLocation.Directory?.FullName!, "discord.png")).GetWrapOrDefault();
    if (discord == null) return;
    using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
    {
      using (ImRaii.Disabled(_dataService.DataDirectory == null))
      {
        if (ImGui.ImageButton(discord.Handle, ScaledVector2(42, 42)))
        {
          Util.OpenLink("https://discord.gg/jX2vxDRkyq");
        }
      }
    }

    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Join Our Discord Community");

    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
    {
      double currentTime = ImGui.GetTime();
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
        if (SelectedTab == ConfigWindowTab.Debug || SelectedTab == ConfigWindowTab.SelfTest)
          SelectedTab = ConfigWindowTab.Overview;
        _logger.Debug("Toggled Debug Mode");
      }
    }
  }

  private void DrawImageButton(ConfigWindowTab tab, string tabName, ImTextureID imageHandle)
  {
    ImGuiStylePtr style = ImGui.GetStyle();
    if (SelectedTab == tab)
    {
      ImDrawListPtr drawList = ImGui.GetWindowDrawList();
      Vector2 screenPos = ImGui.GetCursorScreenPos();
      Vector2 rectMin = screenPos + new Vector2(style.FramePadding.X - 1);
      Vector2 rectMax = screenPos + new Vector2(ScaledFloat(42) + style.FramePadding.X + 1);
      uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.7f, 1.0f, 1.0f));
      drawList.AddRect(rectMin, rectMax, borderColor, 5.0f, ImDrawFlags.None, 2.0f);
    }

    using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
    {
      Vector4 tintColor = (SelectedTab == tab) ? new Vector4(0.6f, 0.8f, 1.0f, 1.0f) : new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

      using (ImRaii.Disabled(_dataService.DataDirectory == null && tab != ConfigWindowTab.Overview))
      {
        if (ImGui.ImageButton(imageHandle, ScaledVector2(42, 42), Vector2.Zero, Vector2.One, (int)style.FramePadding.X, Vector4.Zero, tintColor)) SelectedTab = tab;
      }

      if (ImGui.IsItemHovered())
        using (ImRaii.Tooltip())
          ImGui.Text(tabName);
    }
  }

  private ImTextureID GetImGuiHandleForIconId(uint iconId)
  {
    if (_textureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out ISharedImmediateTexture? icon))
      return icon.GetWrapOrEmpty().Handle;
    return 0;
  }

  private void DrawConfigCheckbox(string label, ref bool value, bool showLabel = true)
  {
    if (ImGui.Checkbox($"##{label.Replace(" ", "").ToLower()}", ref value))
      _configuration.Save();

    if (showLabel)
    {
      ImGui.SameLine();
      ImGui.Text(label);
    }
  }

  private void DrawConfigSlider(string label, ref int value, int min, int max)
  {
    if (ImGui.SliderInt($"##{label.Replace(" ", "").ToLower()}", ref value, min, max, value.ToString()))
      _configuration.Save();

    ImGui.SameLine();
    ImGui.Text(label);
  }

  private void DrawInputPrompt()
  {
    if (!_promptOpen) return;

    ImGui.SetCursorPos(new(0, 0));

    Vector2 windowSize = ImGui.GetContentRegionAvail();
    Vector4 overlayColor = new(0f, 0f, 0f, 0.75f);
    Vector4 promptColor = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];

    using (ImRaii.PushColor(ImGuiCol.ChildBg, overlayColor))
    {
      using (ImRaii.IEndObject overlay = ImRaii.Child("##inputPromptOverlay", windowSize, false))
      {
        if (!overlay.Success) return;

        Vector2 promptSize = ScaledVector2(300, 105);
        Vector2 promptPos = (windowSize - promptSize) / 2.0f;
        ImGui.SetCursorPos(new(promptPos.X + ScaledFloat(25), promptPos.Y));

        using (ImRaii.PushColor(ImGuiCol.ChildBg, promptColor))
        {
          using (ImRaii.IEndObject prompt = ImRaii.Child("##inputPrompt", promptSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
          {
            if (!prompt.Success) return;
            ImGui.TextWrapped(_promptDescription);
            ImGui.Dummy(ScaledVector2(0, 5));
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X);
            ImGui.InputText("##input", ref _promptInputBuffer, 256);

            ImGui.Dummy(ScaledVector2(0, 5));
            if (ImGui.Button("Submit"))
            {
              _promptCallback?.Invoke(_promptInputBuffer);
              _promptOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
              _promptOpen = false;
            }
          }
        }
      }
    }
  }

  private void OpenInputPrompt(string description, string defaultValue, Action<string> callback)
  {
    _promptDescription = description;
    _promptInputBuffer = defaultValue;
    _promptCallback = callback;
    _promptOpen = true;
  }

  private void DrawConfigText(string label, string description, string value, Action<string> callback)
  {
    if (ImGuiComponents.IconButton($"{label.Replace(" ", "")}", Dalamud.Interface.FontAwesomeIcon.Edit, new(20)))
      OpenInputPrompt(description, value, callback);
    ImGui.SameLine();
    ImGui.Text($"{label}: {value}");
  }
}
