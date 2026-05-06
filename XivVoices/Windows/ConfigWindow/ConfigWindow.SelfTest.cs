using Dalamud.Interface.Components;

namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawSelfTestTab()
  {
    using (ImRaii.TableDisposable table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
    {
      if (!table) return;

      ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, ScaledFloat(80));
      ImGui.TableSetupColumn(string.Empty);

      ImGui.TableHeadersRow();

      foreach (SelfTestStep step in Enum.GetValues<SelfTestStep>())
      {
        if (step != SelfTestStep.None)
          DrawStep(step);
      }
    }

    ImGui.Separator();
    ImGui.TextWrapped(_selfTestService.CurrentInstruction);

    bool started = _selfTestService.Step != SelfTestStep.None;
    if (ImGui.Button((started ? "Stop" : "Start") + " Self-Test"))
    {
      if (started) _selfTestService.Stop();
      else _selfTestService.Start();
    }

    foreach ((string button, bool enabled, System.Action action) in _selfTestService.GetButtonsForCurrentStep())
    {
      ImGui.SameLine();
      using (ImRaii.Disabled(!enabled))
        if (ImGui.Button(button))
          action();
    }

    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Separator();
    ImGui.Dummy(ScaledVector2(0, 5));

    string l = "";
    lock (_selfTestService.CurrentLogsLock)
    {
      foreach (string log in _selfTestService.CurrentLogs) l += $"{log}\n";
    }
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
