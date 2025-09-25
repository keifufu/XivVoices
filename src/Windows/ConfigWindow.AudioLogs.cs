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

      IEnumerable<(XivMessage message, bool isPlaying, float percentage, bool isQueued)> history = _playbackService.GetPlaybackHistory();
      if (!history.Any())
      {
        ImGui.Text("There are no voicelines in your history.");
        return;
      }

      foreach ((XivMessage message, bool isPlaying, float percentage, bool isQueued) in history)
      {
        ImGui.TextWrapped($"{message.RawSpeaker}: {message.RawSentence}");

        float progressSize = 245;
        Vector4 plotHistogramColor = _green;

        bool allowReports = message.Source != MessageSource.ChatMessage;
        if (!allowReports) progressSize = 270;
        if (message.IsLocalTTS) plotHistogramColor = _yellow;

        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, plotHistogramColor))
        {
          ImGui.ProgressBar(percentage, ScaledVector2(progressSize, 24), isQueued ? "queued" : message.IsGenerating ? "generating" : isPlaying ? "playing" : "stopped");
        }

        if (allowReports)
        {
          ImGui.SameLine();
          using (ImRaii.Disabled(message.Reported))
          {
            if (ImGuiComponents.IconButton($"##reportButton-{message.Id}", Dalamud.Interface.FontAwesomeIcon.Flag, new(24)))
            {
              OpenInputPrompt("Report Reason", "", (value) =>
              {
                message.Reported = true;
                _reportService.ReportWithReason(message, value);
              });
            }
          }
        }

        ImGui.SameLine();
        if (message.IsGenerating || isQueued)
        {
          if (ImGui.Button("Skip" + $"##controlButton-{message.Id}", ScaledVector2(50, 24)))
          {
            _playbackService.SkipQueuedLine(message);
          }
        }
        else
        {
          if (ImGui.Button(isPlaying ? "Stop" : "Play" + $"##controlButton-{message.Id}", ScaledVector2(50, 24)))
          {
            if (isPlaying)
              _playbackService.Stop(message.Id);
            else
              _ = _playbackService.Play(message, true);
          }
        }

        ImGui.Dummy(ScaledVector2(0, 10));
      }
    }
  }
}
