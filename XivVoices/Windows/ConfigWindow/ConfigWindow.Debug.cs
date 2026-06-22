namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawDebugTab()
  {
    ImGui.Text($"Currently Playing: {_playbackService.Debug_GetPlaying().Count()}");
    ImGui.Text($"Mixer Sources: {_playbackService.Debug_GetMixerSourceCount()}");

    DrawConfigCheckbox("Debug Logging", ref _configuration.DebugLogging);

    DrawConfigText("ServerUrl", "ServerUrl", _dataService.ServerUrl, (value) =>
    {
      _dataService.SetServerUrl(value);
    });

    DrawConfigCheckbox("LiveMode", ref _configuration.LiveMode);
    DrawConfigCheckbox("WarnIgnoredSpeaker", ref _configuration.WarnIgnoredSpeaker);
  }
}
