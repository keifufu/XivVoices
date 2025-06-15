namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawDebugTab()
  {
    ImGui.TextUnformatted($"Currently Playing: {_playbackService.Debug_GetPlaying().Count()}");
    ImGui.TextUnformatted($"Mixer Sources: {_playbackService.Debug_GetMixerSourceCount()}");

    DrawConfigCheckbox("Debug Logging", ref _configuration.DebugLogging);

    DrawConfigText("ServerUrl", "ServerUrl", _dataService.ServerUrl, (ok, value) =>
    {
      if (!ok) return;
      _dataService.SetServerUrl(value);
    });

    DrawConfigText("LocalTTSVoiceMale", "LocalTTSVoiceMale (needs restart)", _configuration.LocalTTSVoiceMale, (ok, value) =>
    {
      if (!ok) return;
      _configuration.LocalTTSVoiceMale = value;
      _configuration.Save();
    });

    DrawConfigText("LocalTTSVoiceFemale", "LocalTTSVoiceFemale (needs restart)", _configuration.LocalTTSVoiceFemale, (ok, value) =>
    {
      if (!ok) return;
      _configuration.LocalTTSVoiceFemale = value;
      _configuration.Save();
    });
  }
}
