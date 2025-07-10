namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawDebugTab()
  {
    ImGui.TextUnformatted($"Currently Playing: {_playbackService.Debug_GetPlaying().Count()}");
    ImGui.TextUnformatted($"Mixer Sources: {_playbackService.Debug_GetMixerSourceCount()}");

    DrawConfigCheckbox("Debug Logging", ref _configuration.DebugLogging);

    DrawConfigText("ServerUrl", "ServerUrl", _dataService.ServerUrl, (value) =>
    {
      _dataService.SetServerUrl(value);
    });

    // TODO: move these to the other local tts settings
    // make them a dropdown which shows you all options that are in the tools folder ending with .bytes and .config.json
    // add a reload button next to it so the list updates if you add more voices locally
    // localttsengine should then also handle voices not being there in case they get deleted
    DrawConfigText("LocalTTSVoiceMale", "LocalTTSVoiceMale (needs restart)", _configuration.LocalTTSVoiceMale, (value) =>
    {
      _configuration.LocalTTSVoiceMale = value;
      _configuration.Save();
    });

    DrawConfigText("LocalTTSVoiceFemale", "LocalTTSVoiceFemale (needs restart)", _configuration.LocalTTSVoiceFemale, (value) =>
    {
      _configuration.LocalTTSVoiceFemale = value;
      _configuration.Save();
    });
  }
}
