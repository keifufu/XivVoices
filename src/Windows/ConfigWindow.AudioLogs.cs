using Dalamud.Interface.Components;

namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawAudioLogsTab()
  {
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Report Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Enable Automatic Reports", ref _configuration.EnableAutomaticReports);
    DrawConfigCheckbox("Log Reports to Chat", ref _configuration.LogReportsToChat);
    ImGui.Dummy(ScaledVector2(0, 5));

    ImGui.Unindent(ScaledFloat(8));
    using (ImRaii.IEndObject child = ImRaii.Child("##AudioLogsScrollingRegion", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
    {
      if (!child.Success) return;

      IEnumerable<(XivMessage message, bool isPlaying, float percentage)> history = _playbackService.GetPlaybackHistory();
      if (!history.Any())
      {
        ImGui.TextUnformatted("There are no voicelines in your history.");
        return;
      }

      foreach ((XivMessage message, bool isPlaying, float percentage) in history)
      {
        ImGui.TextWrapped($"{message.Speaker}: {message.Sentence}");

        float progressSize = 245;
        Vector4 plotHistogramColor = _green;

        if (message.IsLocalTTS)
        {
          plotHistogramColor = _yellow;
          progressSize = 290;
        }

        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, plotHistogramColor))
        {
          ImGui.ProgressBar(percentage, ScaledVector2(progressSize, 24), isPlaying ? "playing" : "stopped");
        }

        ImGui.SameLine();
        if (ImGui.Button(isPlaying ? "Stop" : "Play" + $"##controlButton-{message.Id}", ScaledVector2(50, 24)))
        {
          if (isPlaying)
            _playbackService.Stop(message.Id);
          else
            _ = _playbackService.Play(message, true);
        }

        if (!message.IsLocalTTS)
        {
          ImGui.SameLine();
          using (ImRaii.Disabled(message.Reported))
          {
            if (ImGuiComponents.IconButton($"##reportButton-{message.Id}", Dalamud.Interface.FontAwesomeIcon.Flag, new(24)))
            {
              OpenInputPrompt("Report Reason", "", (ok, value) =>
              {
                if (!ok) return;
                message.Reported = true;
                _reportService.ReportWithReason(message, value);
              });
            }
          }
        }

        ImGui.Dummy(ScaledVector2(0, 10));
      }
    }
  }
}
