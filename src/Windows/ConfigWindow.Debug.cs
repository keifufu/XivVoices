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

    // TODO: move these to the other local tts settings
    // make them a dropdown which shows you all options that are in the tools folder ending with .bytes and .config.json
    // add a reload button next to it so the list updates if you add more voices locally
    // localttsengine should then also handle voices not being there in case they get deleted

    // "custom" models should be stored in another folder to not be overwritten by tools.zip update
    // localtts should also just fall back to the default models that will likely always exist in tools.
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

    DrawConfigCheckbox("UseStreamElementsLocalTTS", ref _configuration.UseStreamElementsLocalTTS);

    // See https://lazypy.ro/tts for all available StreamElements voices.
    // Some have different names than what the api expects, check network traffic for those.
    DrawConfigText("StreamElementsMaleVoice", "StreamElementsMaleVoice", _configuration.StreamElementsMaleVoice, (value) =>
    {
      _configuration.StreamElementsMaleVoice = value;
      _configuration.Save();
    });

    DrawConfigText("StreamElementsFemaleVoice", "StreamElementsFemaleVoice", _configuration.StreamElementsFemaleVoice, (value) =>
    {
      _configuration.StreamElementsFemaleVoice = value;
      _configuration.Save();
    });

    DrawConfigCheckbox("Enable Local Generation", ref _configuration.EnableLocalGeneration);
    DrawConfigCheckbox("Force Local Generation", ref _configuration.ForceLocalGeneration);
    DrawConfigCheckbox("Limit FPS during Local Generation", ref _configuration.LimitFpsDuringLocalGeneration);

    DrawConfigText("LocalGenerationUri", "LocalGenerationUri", _configuration.LocalGenerationUri, (value) =>
    {
      _configuration.LocalGenerationUri = value;
      _configuration.Save();
    });
  }
}
