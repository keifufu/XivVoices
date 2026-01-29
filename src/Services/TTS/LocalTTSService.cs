namespace XivVoices.Services;

public interface ILocalTTSService : IDisposable
{
  Task<string?> WriteLocalTTSToDisk(XivMessage message);
}

public partial class LocalTTSService(ILogger _logger, Configuration _configuration, IDataService _dataService, IGameInteropService _gameInteropService, IObjectTable _objectTable) : ILocalTTSService
{
  private LocalTTSEngine? _localTTSEngine = null;

  public void Dispose()
  {
    _localTTSEngine?.Dispose();
    _localTTSEngine = null;
  }

  private async Task<string?> WriteStreamElementsTTSToDisk(XivMessage message, int speaker, string cleanedSentence)
  {
    if (message.GenerationToken.IsCancellationRequested) return null;

    string voice = speaker == 0 ? _configuration.StreamElementsMaleVoice : _configuration.StreamElementsFemaleVoice;
    string requestUri = $"https://api.streamelements.com/kappa/v2/speech?voice={voice}&text={cleanedSentence}";

    HttpResponseMessage response = await _dataService.HttpClient.GetAsync(requestUri, message.GenerationToken.Token);

    if (response.IsSuccessStatusCode)
    {
      string? tempFilePath = _dataService.TempFilePath($"localtts-se-{Guid.NewGuid()}.mp3");
      if (tempFilePath == null) return null;

      using (FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        await response.Content.CopyToAsync(fileStream);

      return tempFilePath;
    }

    return null;
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
    string cleanedSentence = Regex.Replace(message.AddName(message.Sentence), "[“”]", "\"");
    cleanedSentence = Regex.Replace(cleanedSentence, @"[^\u0000-\u007F]+", "").Trim();
    if (string.IsNullOrEmpty(cleanedSentence))
    {
      _logger.Debug($"Cleaned sentence is empty: {message.Sentence}");
      return null;
    }

    if (message.Source == MessageSource.ChatMessage)
      cleanedSentence = await ProcessPlayerChat(cleanedSentence, message.Speaker);
    cleanedSentence = ApplyLexicon(cleanedSentence);

    // Workaround: all lowercase for now, reason:
    // Felicitous Furball: WeLl, WeLl, WeLl, If It IsN't ThE oWnEr HeRsElF. iT sHaLl Be My PlEaSuRe To AdViSe YoU, wHeThEr YoU dEsIrE iT oR nOt. Oh YeS, i ThInK wE'lL gEt AlOnG jUsT fInE.
    cleanedSentence = cleanedSentence.ToLower();

    int speaker = _configuration.LocalTTSDefaultVoice == "Male" ? 0 : 1;
    if (message.Npc != null) speaker = message.Npc.Gender == "Male" ? 0 : 1;

    if (_configuration.UseStreamElementsLocalTTS)
    {
      string? seFilePath = await WriteStreamElementsTTSToDisk(message, speaker, cleanedSentence);
      if (seFilePath != null) return seFilePath;
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
