using Dalamud.Interface.Components;

namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawSelfTestTab()
  {
    using (ImRaii.IEndObject table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
    {
      if (!table) return;

      ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, ScaledFloat(80));
      ImGui.TableSetupColumn(string.Empty);

      ImGui.TableHeadersRow();

      DrawStep(SelfTestStep.SoundFilter_GetResourceSync);
      DrawStep(SelfTestStep.SoundFilter_GetResourceAsync);
      DrawStep(SelfTestStep.SoundFilter_LoadSoundFile);
      DrawStep(SelfTestStep.SoundFilter_PlaySpecificSound);
      DrawStep(SelfTestStep.Provider_Chat);
      DrawStep(SelfTestStep.Provider_Talk);
      DrawStep(SelfTestStep.Provider_Talk_AutoAdvance);
      DrawStep(SelfTestStep.Interop_GetNpcData);
      DrawStep(SelfTestStep.LipSync);
      DrawStep(SelfTestStep.Provider_MiniTalk);
      DrawStep(SelfTestStep.Interop_GetLocation);
      DrawStep(SelfTestStep.Interop_IsTargetingSummoningBell);
      DrawStep(SelfTestStep.Interop_GetActiveQuests);
      DrawStep(SelfTestStep.Interop_GetActiveLeves);
      DrawStep(SelfTestStep.Interop_Camera);
      DrawStep(SelfTestStep.Provider_BattleTalk);
      DrawStep(SelfTestStep.Interop_IsInDuty);
      DrawStep(SelfTestStep.Interop_IsInCutscene);
    }

    ImGui.Separator();
    ImGui.TextWrapped(_selfTestService.CurrentInstruction);

    bool started = _selfTestService.Step != SelfTestStep.None;
    if (ImGui.Button((started ? "Stop" : "Start") + " Self-Test"))
    {
      if (started) _selfTestService.Stop();
      else _selfTestService.Start();
    }

    bool completeDisabled = false;
    switch (_selfTestService.Step)
    {
      case SelfTestStep.SoundFilter_GetResourceSync:
      case SelfTestStep.SoundFilter_GetResourceAsync:
      case SelfTestStep.SoundFilter_LoadSoundFile:
      case SelfTestStep.SoundFilter_PlaySpecificSound:
      case SelfTestStep.Provider_Talk_AutoAdvance:
      case SelfTestStep.Interop_GetActiveQuests:
      case SelfTestStep.Interop_GetActiveLeves:
        completeDisabled = _selfTestService.StepState != 2;
        goto case SelfTestStep.Interop_Camera;
      case SelfTestStep.Interop_Camera:
        ImGui.SameLine();
        using (ImRaii.Disabled(completeDisabled))
          if (ImGui.Button("Complete"))
            _selfTestService.Next(true, false);
        ImGui.SameLine();
        if (ImGui.Button("Skip"))
          _selfTestService.Next(false, true);
        break;
      case SelfTestStep.LipSync:
        ImGui.SameLine();
        if (ImGui.Button("LipSync Target"))
          _selfTestService.LipSyncTarget();
        goto case SelfTestStep.Interop_Camera; // Draw Complete and Skip buttons
    }

    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Separator();
    ImGui.Dummy(ScaledVector2(0, 5));

    string l = "";
    foreach (string log in _selfTestService.CurrentLogs) l += $"{log}\n";
    ImGui.InputTextMultiline("##SelfTestLogs", ref l, 1024, ScaledVector2(350, 120), ImGuiInputTextFlags.ReadOnly);
  }

  private void DrawStep(SelfTestStep state)
  {
    bool active = _selfTestService.Step == state;
    bool skipped = (_selfTestService.SkippedTests & state) == state;
    bool completed = (_selfTestService.CompletedTests & state) == state;

    ImGui.TableNextRow();
    ImGui.TableNextColumn();
    ImGui.Text(state.ToString());

    ImGui.TableNextColumn();
    Vector4 c = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
    using (ImRaii.PushColor(ImGuiCol.Text, active ? _yellow : completed ? _green : skipped ? _lightgrey : c))
    {
      ImGui.Text(active ? "Running" : skipped ? "Skipped" : completed ? "Completed" : "Pending");
    }

    ImGui.TableNextColumn();
    if (ImGuiComponents.IconButton($"##skipTo-{state}", Dalamud.Interface.FontAwesomeIcon.FastForward, new(20)))
      _selfTestService.SkipTo(state);

    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text($"Skip to {state}");
  }
}
