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

    DrawConfigText("StreamElementsApiKey", "StreamElementsApiKey", _configuration.StreamElementsApiKey, (value) =>
    {
      _configuration.StreamElementsApiKey = value;
      _configuration.Save();
    });

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

    DrawConfigCheckbox("SuperFastForward", ref _configuration.SuperFastForward);

    DrawConfigCheckbox("LiveMode", ref _configuration.LiveMode);
    DrawConfigCheckbox("WarnIgnoredSpeaker", ref _configuration.WarnIgnoredSpeaker);
  }
}
