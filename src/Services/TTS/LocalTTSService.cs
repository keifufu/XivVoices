namespace XivVoices.Services;

public interface ILocalTTSService : IDisposable
{
  Task<string?> WriteLocalTTSToDisk(XivMessage message);
}

public partial class LocalTTSService(ILogger _logger, Configuration _configuration, IDataService _dataService, IGameInteropService _gameInteropService, IClientState _clientState) : ILocalTTSService
{
  private LocalTTSEngine? _localTTSEngine = null;

  public void Dispose()
  {
    _localTTSEngine?.Dispose();
    _localTTSEngine = null;
  }

  // returns a path to the temporary .wav output
  // this path should be deleted once you're done with it.
  // can return null if it failed to generate.
  public async Task<string?> WriteLocalTTSToDisk(XivMessage message)
  {
    if (message.GenerationToken.IsCancellationRequested) return null;

    string? dataDirectory = _dataService.DataDirectory;
    string? toolsDirectory = _dataService.ToolsDirectory;
    if (dataDirectory == null || toolsDirectory == null) return null;

    // Something in our implementation can only handle ascii. I don't think
    // that's on piper but probably something in our wrapper (localtts.dll)
    string cleanedSentence = Regex.Replace(message.Sentence, "[“”]", "\"");
    cleanedSentence = Regex.Replace(cleanedSentence, @"[^\u0000-\u007F]+", "").Trim();
    if (string.IsNullOrEmpty(cleanedSentence))
    {
      _logger.Debug($"Cleaned sentence is empty: {message.Sentence}");
      return null;
    }

    if (message.Source == MessageSource.ChatMessage)
      cleanedSentence = await ProcessPlayerChat(cleanedSentence, message.Speaker);
    cleanedSentence = ApplyLexicon(cleanedSentence);

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
    if (message.Npc != null) speaker = message.Npc.Gender == "Male" ? 0 : 1;

    if (_localTTSEngine.Voices[speaker] == null)
    {
      _logger.Debug($"LocalTTSVoice {speaker} was not loaded");
      return null;
    }

    if (message.GenerationToken.IsCancellationRequested) return null;

    float[] pcmData = await _localTTSEngine.SpeakTTS(cleanedSentence, _localTTSEngine.Voices[speaker]!);
    if (pcmData.Length == 0) return null;

    WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(22050, 1);
    string? tempFilePath = _dataService.TempFilePath($"localtts-{Guid.NewGuid()}.wav");
    if (tempFilePath == null) return null;
    using (WaveFileWriter waveFileWriter = new(tempFilePath, waveFormat))
    {
      foreach (float sample in pcmData)
        waveFileWriter.WriteSample(sample);
    }

    if (message.GenerationToken.IsCancellationRequested) return null;

    return tempFilePath;
  }
}
