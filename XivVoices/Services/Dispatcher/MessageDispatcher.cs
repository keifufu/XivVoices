namespace XivVoices.Services;

public interface IMessageDispatcher : IHostedService
{
  Task TryDispatch(MessageSource source, string rawSpeaker, string rawSentence, uint? speakerBaseId = null, bool isFake = false, string? voiceOverride = null, int? pitchOverride = null, string? speakerWorld = null, XivChatType? chatChannel = null);
  void ClearQueue();
  void Prev();
  string ReplaceName(string sentence, string playerName);
  (string speaker, string sentence) CleanMessage(string _speaker, string _sentence, string playerName, bool legacyNameReplacement);
  void DispatchTestMessage();
  void DispatchLocalTTSMessage(string voice, int pitch, string sentence);
}

public enum PlaybackQueueState
{
  Playing,
  Stopped
}

public class PlaybackQueue
{
  public object QueueLock = new();
  public List<XivMessage> Queue { get; set; } = [];
  public PlaybackQueueState PlaybackQueueState { get; set; } = PlaybackQueueState.Stopped;
  public MessageSource MessageSource { get; set; }
}

public partial class MessageDispatcher(ILogger _logger, Configuration _configuration, IDataService _dataService, ISoundFilter _soundFilter, IReportService _reportService, IPlaybackService _playbackService, IGameInteropService _gameInteropService, IFramework _framework, IClientState _clientState) : IMessageDispatcher
{
  private Dictionary<MessageSource, PlaybackQueue> _queues = [];
  private InterceptedSound? _interceptedSound;

  public Task StartAsync(CancellationToken token)
  {
    foreach (MessageSource source in Enum.GetValues<MessageSource>())
      _queues[source] = new() { MessageSource = source };

    _soundFilter.OnVoicelineDetected += SoundFilter_OnCutSceneAudioDetected;

    _framework.Update += OnFrameworkUpdate;
    _playbackService.PlaybackCompleted += OnPlaybackCompleted;
    _playbackService.QueuedLineSkipped += OnQueuedLineSkipped;

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken token)
  {
    _soundFilter.OnVoicelineDetected -= SoundFilter_OnCutSceneAudioDetected;

    _framework.Update -= OnFrameworkUpdate;
    _playbackService.PlaybackCompleted -= OnPlaybackCompleted;
    _playbackService.QueuedLineSkipped -= OnQueuedLineSkipped;

    _queues = [];

    return _logger.ServiceLifecycle();
  }

  public void ClearQueue()
  {
    foreach (PlaybackQueue playbackQueue in _queues.Values)
    {
      playbackQueue.Queue.Clear();
    }
  }

  private XivMessage? _prevPlaying;
  public void Prev()
  {
    _logger.Debug("Replaying previous voiceline");
    _playbackService.Stop(MessageSource.AddonBattleTalk);

    XivMessage? prev = _playbackService.GetPrev(_prevPlaying);
    if (prev == null)
    {
      _logger.Debug("No previous voiceline found to replay");
      return;
    }

    if (_playbackService.SeekToStart()) return;

    _prevPlaying = prev;
    List<XivMessage> stopped = _playbackService.StopAll();
    foreach (XivMessage message in stopped)
    {
      if (message.Queued)
      {
        message.Replay = true;
        lock (_queues[GetQueueForMessage(message)].QueueLock)
        {
          _queues[GetQueueForMessage(message)].Queue.Insert(0, message);
        }
      }
    }
    _playbackService.Play(prev, true);
  }

  private bool TryDequeue(PlaybackQueue queue, out XivMessage message)
  {
    lock (queue.QueueLock)
    {
      if (queue.Queue.Count == 0)
      {
        message = null!;
        return false;
      }

      message = queue.Queue[0];
      queue.Queue.RemoveAt(0);
      return true;
    }
  }

  private void OnFrameworkUpdate(IFramework framework)
  {
    foreach (PlaybackQueue playbackQueue in _queues.Values)
    {
      lock (playbackQueue.QueueLock)
      {
        if (playbackQueue.PlaybackQueueState == PlaybackQueueState.Stopped && _prevPlaying == null)
        {
          if (!_playbackService.Paused && TryDequeue(playbackQueue, out XivMessage? message))
          {
            _logger.Debug($"Playing queued message: {message.Id}");
            _playbackService.RemoveQueuedLine(message);
            playbackQueue.PlaybackQueueState = PlaybackQueueState.Playing;
            _ = _playbackService.Play(message, message.Replay);
          }
        }
      }
    }
  }

  private void OnPlaybackCompleted(object? sender, XivMessage message)
  {
    if (message.Id == _prevPlaying?.Id) _prevPlaying = null;

    int count = _playbackService.CountPlaying(GetQueueForMessage(message));
    if (GetQueueForMessage(message) == MessageSource.AddonTalk)
      count += _playbackService.CountPlaying(MessageSource.SelectString);

    if (count == 0)
    {
      _logger.Debug($"{GetQueueForMessage(message)} Playback Completed.");
      _queues[GetQueueForMessage(message)].PlaybackQueueState = PlaybackQueueState.Stopped;
    }
    else
    {
      _logger.Debug($"{GetQueueForMessage(message)} Playback Completed, but {count} still playing.");
    }
  }

  private void OnQueuedLineSkipped(object? sender, XivMessage message)
  {
    lock (_queues[GetQueueForMessage(message)].QueueLock)
    {
      _queues[GetQueueForMessage(message)].Queue.Remove(message);
      _logger.Debug($"Removed queued line: {message.Id}");
    }
  }

  private void SoundFilter_OnCutSceneAudioDetected(object? sender, InterceptedSound sound)
  {
    if (_dataService.Manifest == null) return;
    if (!_clientState.IsLoggedIn || !(_gameInteropService.IsInCutscene() || _gameInteropService.IsInDuty())) return;
    _logger.Debug($"SoundFilter: {sound.ShouldBlock} {sound.SoundPath}");
    _interceptedSound = sound;
  }

  public async Task TryDispatch(MessageSource source, string rawSpeaker, string rawSentence, uint? speakerBaseId = null, bool isFake = false, string? voiceOverride = null, int? pitchOverride = null, string? speakerWorld = null, XivChatType? chatChannel = null)
  {
    if (_dataService.Manifest == null) return;
    string speaker = rawSpeaker;
    string sentence = rawSentence;

    if ((_gameInteropService.IsInCutscene() && source == MessageSource.AddonTalk) || (_gameInteropService.IsInDuty() && (source == MessageSource.AddonBattleTalk || source == MessageSource.AddonMiniTalk)))
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

    string? playerName = _gameInteropService.PlayerName;
    if (playerName == null)
    {
      if (isFake) playerName = "Fake Name";
      else return; // Nah
    }
    bool sentenceHasName = SentenceHasPlayerName(sentence, playerName);

    // If this sentence matches a sentence in Manifest.DirectMappings.Retainer,
    // then replace the speaker with the retainer one.
    // This needs to be checked before CleanMessage.
    bool mappedNpcFound = false;
    NpcEntry? mappedNpc = null;
    bool isRetainer = false;
    if (source == MessageSource.AddonTalk && _gameInteropService.IsOccupiedBySummoningBell())
    {
      _logger.Debug("AddonTalk message is from a retainer");
      isRetainer = true;

      (_, string sentenceToMatch) = CleanMessage(speaker, sentence, playerName, true);
      (mappedNpcFound, mappedNpc) = GetNpcFromMappings(SpeakerMappingType.Retainer, null, sentenceToMatch);
      if (mappedNpcFound) _logger.Debug("Found mapped retainer npc");
      else _logger.Debug("Failed to find mapped retainer npc");
    }

    // Check if npc mappings have this sentence/speaker combo
    // but only if we don't already have a mappedNpc (from retainers)
    if (mappedNpc == null)
    {
      // First, check with legacy name replacements
      (string speakerToMatch, string sentenceToMatch) = CleanMessage(speaker, sentence, playerName, true);
      (mappedNpcFound, mappedNpc) = GetNpcFromMappings(SpeakerMappingType.Nameless, speakerToMatch, sentenceToMatch);

      // Try with new name replacements only if it didn't find a mapped NPC and the sentence has the player name.
      if (!mappedNpcFound && sentenceHasName)
      {
        (speakerToMatch, sentenceToMatch) = CleanMessage(speaker, sentence, playerName, false);
        (mappedNpcFound, mappedNpc) = GetNpcFromMappings(SpeakerMappingType.Nameless, speakerToMatch, sentenceToMatch);
      }

      if (mappedNpcFound) _logger.Debug("Found mapped nameless npc");
    }

    if (mappedNpcFound && mappedNpc == null)
    {
      _logger.Debug("Mapped npc returned null. Skipping this voiceline");
      return;
    }

    // Clean message and replace names with new replacement method.
    (string cleanedSpeaker, string cleanedSentence) = CleanMessage(speaker, sentence, playerName, false);

    NpcEntry? npc = mappedNpc ?? GetNpc(source, cleanedSpeaker);
    if (npc == null || npc.HasVariedLooks)
      npc = await _gameInteropService.RunOnFrameworkThread(() => _gameInteropService.TryGetNpc(speaker, speakerBaseId, npc));

    VoiceEntry? voice = null;
    if (!(npc?.HasVariedLooks ?? false) && _dataService.Manifest.Voices.TryGetValue(npc?.VoiceId ?? "", out VoiceEntry? _voice))
      voice = _voice;
    else
    {
      voice = GetGenericVoice(npc);

      // Set the VoiceId so the report accurately represents the voice we expect the line to be.
      if (source != MessageSource.ChatMessage && source != MessageSource.SelectString && voice != null && npc != null)
        npc.VoiceId = voice.Id;
    }

    // Cache player npc to assign a gender to chatmessage tts when they're not near you.
    if ((source == MessageSource.ChatMessage || source == MessageSource.SelectString) && npc != null)
      _dataService.CachePlayer(rawSpeaker, npc);

    // Try to retrieve said cached npc if they're not near you.
    if ((source == MessageSource.ChatMessage || source == MessageSource.SelectString) && npc == null)
      npc = _dataService.TryGetCachedPlayer(rawSpeaker);

    // Try and find a voiceline.
    (string id, string? voicelinePath) = await TryGetVoiceline(voice, npc, cleanedSentence);

    // If no voiceline was found, try again with legacy name replacement method.
    if (voicelinePath == null && sentenceHasName)
    {
      (_, string legacySentence) = CleanMessage(speaker, sentence, playerName, true);
      (id, voicelinePath) = await TryGetVoiceline(voice, npc, legacySentence);
      if (voicelinePath == null) _logger.Debug("Did not find legacy name voiceline");
      else
      {
        _logger.Debug("Found legacy name voiceline");
        cleanedSentence = legacySentence;
      }
    }

    // If we still have no voiceline, try not replacing names at all.
    // This might help in some cases for users with names such as "I'm".
    if (voicelinePath == null && sentenceHasName)
    {
      (_, string fallbackSentence) = CleanMessage(speaker, sentence, "NotAReal Name", false);
      (id, voicelinePath) = await TryGetVoiceline(voice, npc, fallbackSentence);
      if (voicelinePath == null) _logger.Debug("Did not find fallback name voiceline");
      else
      {
        _logger.Debug("Found fallback name voiceline");
        cleanedSentence = fallbackSentence;
      }
    }

    bool forcedLocalTTS = false;
    if (_configuration.LocalTTSForced && voicelinePath != null)
    {
      _logger.Debug("Voiceline was found but LocalTTS was forced");
      forcedLocalTTS = true;
      voicelinePath = null;
    }

    XivMessage message = new(
      id,
      source,
      cleanedSpeaker,
      cleanedSentence,
      rawSpeaker,
      ReplaceName(rawSentence, playerName),
      npc,
      voice,
      voicelinePath,
      playerName,
      isFake,
      voiceOverride,
      pitchOverride,
      speakerWorld,
      chatChannel
    );

    _logger.Debug($"Constructed message: {message}");

    // Report if it's not a chat message, it couldn't find a voiceline and the speaker is not ignored.
    // Retainers are not being reported anymore, as they could have names like "Cahciua" and just be
    // generated as that. I'm not having that so we will do retainer lines manually if we find any missing.
    bool isIgnoredSpeaker = _dataService.Manifest.IgnoredSpeakers.Contains(message.Speaker);

    // See: https://ffxiv.consolegameswiki.com/wiki/Who%27s_Who
    if (message.RawSpeaker == $"{playerName.Split(" ")[0]}?") isIgnoredSpeaker = true;

    if (!isFake && source != MessageSource.ChatMessage && source != MessageSource.SelectString && message.VoicelinePath == null && !isIgnoredSpeaker && !isRetainer && !forcedLocalTTS)
      _reportService.Report(message);

    // If in LiveMode, warn about ignored speakers in chat, but only for addontalk messages.
    if (source == MessageSource.AddonTalk && isIgnoredSpeaker && (_configuration.LiveMode || _configuration.WarnIgnoredSpeaker))
      _logger.Chat(pre: $"Ignored Speaker: {message.Speaker}", preColor: 25);

    bool allowed = true;
    bool isNarrator = message.Speaker == "Narrator";
    switch (source)
    {
      case MessageSource.AddonTalk:
        allowed = _configuration.AddonTalkEnabled
          && (isNarrator
            ? _configuration.AddonTalkNarratorEnabled && (!message.IsLocalTTS || _configuration.AddonTalkTTSEnabled)
            : !message.IsLocalTTS || _configuration.AddonTalkTTSEnabled);
        message.Queued = _configuration.QueueDialogue;
        break;
      case MessageSource.AddonBattleTalk:
        allowed = _configuration.AddonBattleTalkEnabled
          && (isNarrator
            ? _configuration.AddonTalkNarratorEnabled && (!message.IsLocalTTS || _configuration.AddonBattleTalkTTSEnabled)
            : !message.IsLocalTTS || _configuration.AddonBattleTalkTTSEnabled);
        message.Queued = true;
        break;
      case MessageSource.AddonMiniTalk:
        allowed = _configuration.AddonMiniTalkEnabled && (!message.IsLocalTTS || _configuration.AddonMiniTalkTTSEnabled);
        message.Queued = true;
        break;
      case MessageSource.ChatMessage:
        message.Queued = _configuration.QueueChatMessages;
        break;
      case MessageSource.SelectString:
        message.Queued = _configuration.QueueDialogue;
        break;
    }

    if (isFake) allowed = true;

    if (source == MessageSource.AddonMiniTalk)
      _logger.Chat(message.RawSentence, name: npc?.Id ?? "Bubble", channel: XivChatType.NPCDialogue, addPrefix: false);

    if (isNarrator)
      _logger.Chat(message.RawSentence, name: "Narrator", channel: XivChatType.NPCDialogue, addPrefix: false);

    if (source == MessageSource.SelectString)
      _logger.Chat(message.RawSentence, name: playerName, channel: XivChatType.NPCDialogue, addPrefix: false);

    if ((_configuration.MuteEnabled && !isFake) || !allowed || (isRetainer && !_configuration.RetainersEnabled))
    {
      _logger.Debug($"Not playing line due to user configuration. MuteEnabled:{_configuration.MuteEnabled} allowed:{allowed} isNarrator:{isNarrator} isRetainer:{isRetainer} isLocalTTS:{message.IsLocalTTS}");
      return;
    }

    if (!_playbackService.IsOutputDeviceInitialized)
    {
      _logger.Error("Output device is not initialized, not even attempting to queue or play message");
      return;
    }

    if (message.Queued || _playbackService.Paused)
    {
      _logger.Debug($"Queueing message: {message.Id}");
      lock (_queues[GetQueueForMessage(message)].QueueLock)
      {
        _queues[GetQueueForMessage(message)].Queue.Add(message);
        _playbackService.AddQueuedLine(message);
      }
    }
    else
    {
      _logger.Debug($"Playing message: {message.Id}");
      _ = _playbackService.Play(message);
    }
  }

  private MessageSource GetQueueForMessage(XivMessage message)
  {
    // Enqueue SelectString with AddonTalk
    return message.Source == MessageSource.SelectString ? MessageSource.AddonTalk : message.Source;
  }

  public void DispatchTestMessage()
  {
    string sentence = Random.Shared.Next(100) == 0 ? "Testing.... Ardbert, can you hear me? I can speak now. Will you come home soon?" : "This is a test message.";
    _ = TryDispatch(MessageSource.AddonTalk, "Narrator", sentence, null, true);
  }

  public void DispatchLocalTTSMessage(string voice, int pitch, string sentence)
  {
    _ = TryDispatch(MessageSource.AddonTalk, "Preview", sentence, null, true, voice, pitch);
  }
}
