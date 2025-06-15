namespace XivVoices.Services;

public interface ILocalTTSService : IDisposable
{
  Task<string?> WriteLocalTTSToDisk(XivMessage message);
}

public partial class LocalTTSService(ILogger _logger, Configuration _configuration, IDataService _dataService, IFramework _framework, IClientState _clientState) : ILocalTTSService
{
  private LocalTTSEngine? _localTTSEngine = null;

  public void Dispose()
  {
    if (_localTTSEngine != null)
    {
      _localTTSEngine.Dispose();
      _localTTSEngine = null;
    }
  }

  // returns a path to the temporary .wav output
  // this path should be deleted once you're done with it.
  // can return null if it failed to generate.
  public async Task<string?> WriteLocalTTSToDisk(XivMessage message)
  {
    string? dataDirectory = _dataService.DataDirectory;
    string? toolsDirectory = _dataService.ToolsDirectory;
    if (dataDirectory == null || toolsDirectory == null) return null;

    string sentence = Regex.Replace(message.OriginalSentence, "[“”]", "\"");
    if (message.Source == MessageSource.ChatMessage)
      sentence = await ProcessPlayerChat(message);
    sentence = ApplyLexicon(sentence);

    // localtts only really supports english, oh well.
    sentence = new string([.. sentence.Where(c =>
      (c >= 'a' && c <= 'z') ||
      (c >= 'A' && c <= 'Z') ||
      (c >= '0' && c <= '9') ||
      c == ',' ||
      c == '.' ||
      c == ' '
    )]);
    if (!sentence.Any(char.IsLetter))
    {
      _logger.Debug($"Failed to clean local tts message: {message.OriginalSentence} -> {sentence}");
      return null;
    }

    if (_localTTSEngine == null)
    {
      try
      {
        _localTTSEngine = new LocalTTSEngine(toolsDirectory, _logger, _configuration);
      }
      catch (Exception ex)
      {
        _logger.Error(ex);
        return null;
      }
    }

    int speaker = _configuration.LocalTTSDefaultVoice == "Male" ? 0 : 1;
    if (message.NpcData != null) speaker = message.NpcData.Gender == "Male" ? 0 : 1;

    if (_localTTSEngine.Voices[speaker] == null)
    {
      _logger.Debug($"LocalTTSVoice {speaker} was not loaded");
      return null;
    }

    float[] pcmData = await _localTTSEngine.SpeakTTS(sentence, _localTTSEngine.Voices[speaker]!);

    WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(22050, 1);
    string? tempFilePath = _dataService.TempFilePath($"localtts-{Guid.NewGuid()}.wav");
    if (tempFilePath == null) return null;
    using (WaveFileWriter waveFileWriter = new(tempFilePath, waveFormat))
    {
      foreach (float sample in pcmData)
        waveFileWriter.WriteSample(sample);
    }

    return tempFilePath;
  }
}
