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

    // If speaker is ignored, well... ignore it.
    if (_dataService.Manifest.IgnoredSpeakers.Contains(speaker)) return;

    // If this sentence matches a sentence in Manifest.DirectMappings.Retainer,
    // then replace the speaker with the retainer one.
    // This needs to be checked before CleanMessage.
    NpcEntry? mappedNpc = null;
    bool isRetainer = false;
    if (source == MessageSource.AddonTalk)
    {
      bool isTargetingRetainerBell = await _gameInteropService.IsTargetingRetainerBell();
      if (isTargetingRetainerBell)
      {
        mappedNpc = GetNpcFromMappings(SpeakerMappingType.Retainer, sentence);
        if (mappedNpc != null) isRetainer = true;
        if (speaker == "Feo Ul") isRetainer = true;
      }
    }

    if (speaker == "???")
      mappedNpc = GetNpcFromMappings(SpeakerMappingType.Nameless, sentence);

    NpcEntry? npc = mappedNpc ?? GetNpc(speaker);
    if (npc == null || npc.HasVariedLooks)
      npc = await _gameInteropService.TryGetNpc(speaker, speakerBaseId, npc);

    VoiceEntry? voice = null;
    if (!(npc?.HasVariedLooks ?? false) && _dataService.Manifest.Voices.TryGetValue(npc?.VoiceId ?? "", out VoiceEntry? _voice))
      voice = _voice;
    else
    {
      voice = GetGenericVoice(npc);
      // Set the VoiceId so the report accurately represents the voice we expect the line to be.
      if (voice != null && npc != null)
        npc.VoiceId = voice.Id;
    }

    string? playerName = await _framework.RunOnFrameworkThread(() => _clientState.LocalPlayer?.Name.TextValue ?? null);
    bool sentenceHasName = SentenceHasPlayerName(sentence, playerName);

    // Cache player npc to assign a gender to chatmessage tts when they're not near you.
    if (source == MessageSource.ChatMessage && npc != null)
      _dataService.CachePlayer(origSpeaker, npc);

    // Try to retrieve said cached npc if they're not near you.
    if (source == MessageSource.ChatMessage && npc == null)
      npc = _dataService.TryGetCachedPlayer(origSpeaker);

    // Clean message and replace names with new replacement method.
    (string cleanedSpeaker, string cleanedSentence) = CleanMessage(speaker, sentence, playerName, false, false);

    // Try and find a voiceline.
    (string id, string? voicelinePath) = await TryGetVoiceline(voice, npc, cleanedSentence);

    // If no voiceline was found, try again with legacy name replacement method.
    if (voicelinePath == null && sentenceHasName)
    {
      (_, string legacySentence) = CleanMessage(speaker, sentence, playerName, true, false);
      (id, voicelinePath) = await TryGetVoiceline(voice, npc, legacySentence);
      if (voicelinePath == null) _logger.Debug("Did not find legacy name voiceline");
      else _logger.Debug("Found legacy name voiceline");
    }

    // If we still haven't found a voiceline, clean the messages again but keep the name for localtts.
    if (voicelinePath == null && sentenceHasName)
      (cleanedSpeaker, cleanedSentence) = CleanMessage(speaker, sentence, playerName, false, true);

    XivMessage message = new(
      id,
      source,
      voice,
      cleanedSpeaker,
      cleanedSentence,
      origSpeaker,
      origSentence,
      npc,
      voicelinePath
    );

    _logger.Debug($"Constructed message: {message}");

    if (source != MessageSource.ChatMessage && message.VoicelinePath == null)
      _reportService.Report(message);

    bool allowed = true;
    bool isNarrator = message.Speaker == "Narrator";
    switch (source)
    {
      case MessageSource.AddonTalk:
        allowed = _configuration.AddonTalkEnabled
          && (isNarrator
            ? _configuration.AddonTalkNarratorEnabled
            : !message.IsLocalTTS || _configuration.AddonTalkTTSEnabled);
        break;
      case MessageSource.AddonBattleTalk:
        allowed = _configuration.AddonBattleTalkEnabled
          && (isNarrator
            ? _configuration.AddonTalkNarratorEnabled
            : !message.IsLocalTTS || _configuration.AddonBattleTalkTTSEnabled);
        break;
      case MessageSource.AddonMiniTalk:
        allowed = _configuration.AddonMiniTalkEnabled && (!message.IsLocalTTS || _configuration.AddonMiniTalkTTSEnabled);
        break;
    }

    if (isNarrator && _configuration.PrintNarratorMessages)
      _logger.Chat(message.OriginalSentence, "", "", "Narrator", XivChatType.NPCDialogue, false);

    if (_configuration.MuteEnabled || !allowed || (isRetainer && !_configuration.RetainersEnabled) || (message.IsLocalTTS && !_configuration.LocalTTSEnabled))
    {
      _logger.Debug($"Not playing line due to user configuration: {allowed} {isNarrator} {isRetainer} {message.IsLocalTTS}");
      return;
    }

    await _playbackService.Play(message);
  }
}
