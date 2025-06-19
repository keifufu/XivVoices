namespace XivVoices.Services;

public interface IMessageDispatcher : IHostedService
{
  Task TryDispatch(MessageSource source, string origSpeaker, string origSentence, uint? speakerBaseId = null);
}

public partial class MessageDispatcher(ILogger _logger, Configuration _configuration, IDataService _dataService, ISoundFilter _soundFilter, IReportService _reportService, IPlaybackService _playbackService, IGameInteropService _gameInteropService, IFramework _framework, IClientState _clientState) : IMessageDispatcher
{
  private InterceptedSound? _interceptedSound;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _soundFilter.OnCutsceneAudioDetected += SoundFilter_OnCutSceneAudioDetected;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _soundFilter.OnCutsceneAudioDetected -= SoundFilter_OnCutSceneAudioDetected;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void SoundFilter_OnCutSceneAudioDetected(object? sender, InterceptedSound sound)
  {
    if (_dataService.Manifest == null) return;
    if (!_clientState.IsLoggedIn || !(_gameInteropService.IsInCutscene() || _gameInteropService.IsInDuty())) return;
    _logger.Debug($"SoundFilter: {sound.ShouldBlock} {sound.SoundPath}");
    _interceptedSound = sound;
  }

  public async Task TryDispatch(MessageSource source, string origSpeaker, string origSentence, uint? speakerBaseId = null)
  {
    if (_dataService.Manifest == null) return;
    string speaker = origSpeaker;
    string sentence = origSentence;

    if ((source == MessageSource.AddonTalk && _gameInteropService.IsInCutscene()) || source == MessageSource.AddonBattleTalk)
    {
      // SoundFilter is a lil slower than our providers so we wait a bit.
      // This is NOT that great but it works. 100 is an arbitrary number that seems to work for now.
      await Task.Delay(100);
      if (_interceptedSound != null)
      {
        if (_interceptedSound.ShouldBlock && _interceptedSound.IsValid())
        {
          _logger.Debug($"{source} message blocked by SoundFilter");
          _interceptedSound = null;
          return;
        }
        else
        {
          _logger.Debug($"Invalid SoundFilter not used, creation date: {_interceptedSound.CreationDate}");
          _interceptedSound = null;
        }
      }
    }

    // If this sentence matches a sentence in Manifest.Retainers
    // and the speaker is not in Manifest.NpcsWithRetainerLines,
    // then replace the speaker with the retainer one.
    // This needs to be checked before CleanMessage.
    string retainerSpeaker = GetRetainerSpeaker(speaker, sentence);
    bool isRetainer = false;
    if (retainerSpeaker != speaker)
    {
      isRetainer = true;
      speaker = retainerSpeaker;
    }

    // If speaker is ignored, well... ignore it.
    if (_dataService.Manifest.IgnoredSpeakers.Contains(speaker)) return;

    // Clean speaker and sentence only if this is a NPC message.
    if (source != MessageSource.ChatMessage)
    {
      (speaker, sentence) = await CleanMessage(speaker, sentence);

      // Skip if there's nothing meaningful to voice
      // E.g. if the sentence was "..." or "<sigh>"
      if (string.IsNullOrEmpty(sentence))
      {
        _logger.Debug($"Cleaned sentence is empty: {origSentence}");
        return;
      }
    }

    // This one is a bit weird, we try to look up the NpcData directly from the game, that makes sense.
    // But even if we find it, for non-beastmen we prefer the cache? Ok.
    // I belive this is due to solo duties, I was just in one where Korutt had a different appearance
    // inside a duty than outside. Fine.
    // This can be null and a valid voice can still be found from Manifest.Nameless or Manifest.Voices
    NpcData? npcData = await _gameInteropService.TryGetNpcData(speaker, speakerBaseId);
    if (npcData == null || npcData.Body != "Beastman")
    {
      if (_dataService.Manifest.NpcData.TryGetValue(speaker, out NpcData? _npcData))
        npcData = _npcData;
    }

    // Cache player npcData to assign a gender to chatmessage tts when they're not near you.
    if (source == MessageSource.ChatMessage && npcData != null)
      _dataService.CachePlayerNpcData(origSpeaker, npcData);

    // Try to retrieve said cached npcData if they're not near you.
    if (source == MessageSource.ChatMessage && npcData == null)
      npcData = _dataService.TryGetCachedPlayerNpcData(origSpeaker);

    string? voicelinePath = null;
    string? voice = "";
    if (source != MessageSource.ChatMessage)
      (voicelinePath, voice) = await TryGetVoicelinePath(speaker, sentence, npcData);

    // If this line will be LocalTTS, clean message but keep the player name.
    if (voicelinePath == null)
      (speaker, sentence) = await CleanMessage(origSpeaker, origSentence, true);

    XivMessage message = new(
      Md5(speaker, sentence),
      source,
      voice ?? "",
      speaker,
      sentence,
      origSpeaker,
      origSentence,
      npcData,
      voicelinePath
    );

    _logger.Debug($"Constructed message: {message}");

    if (source != MessageSource.ChatMessage && message.VoicelinePath == null)
      _reportService.Report(message);

    bool allowed = true;
    bool isSystemMessage = speaker.StartsWith("Addon");
    switch (source)
    {
      case MessageSource.AddonTalk:
        allowed = _configuration.AddonTalkEnabled
          && (isSystemMessage
            ? _configuration.AddonTalkSystemEnabled
            : !message.IsLocalTTS || _configuration.AddonTalkTTSEnabled);
        break;
      case MessageSource.AddonBattleTalk:
        allowed = _configuration.AddonBattleTalkEnabled
          && (isSystemMessage
            ? _configuration.AddonBattleTalkSystemEnabled
            : !message.IsLocalTTS || _configuration.AddonBattleTalkTTSEnabled);
        break;
      case MessageSource.AddonMiniTalk:
        allowed = _configuration.AddonMiniTalkEnabled && (!message.IsLocalTTS || _configuration.AddonMiniTalkTTSEnabled);
        break;
    }

    if (isSystemMessage && _configuration.PrintSystemMessages)
      _logger.Chat(message.OriginalSentence, "", "", "System", XivChatType.NPCDialogue, false);

    if (_configuration.MuteEnabled || !allowed || (isRetainer && !_configuration.RetainersEnabled) || (message.IsLocalTTS && !_configuration.LocalTTSEnabled))
    {
      _logger.Debug($"Not playing line due to user configuration: {allowed} {isSystemMessage} {isRetainer} {message.IsLocalTTS}");
      return;
    }

    await _playbackService.Play(message);
  }
}
