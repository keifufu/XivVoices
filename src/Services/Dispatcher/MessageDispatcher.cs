namespace XivVoices.Services;

public interface IMessageDispatcher : IHostedService
{
  bool Paused { get; set; }
  Task TryDispatch(MessageSource source, string rawSpeaker, string rawSentence, uint? speakerBaseId = null);
  void ClearQueue();
}

public enum PlaybackQueueState
{
  AwaitingConfirmation,
  Playing,
  Stopped
}

public class PlaybackQueue
{
  public ConcurrentQueue<XivMessage> Queue { get; set; } = new();
  public PlaybackQueueState PlaybackQueueState { get; set; } = PlaybackQueueState.Stopped;
  public DateTime PlaybackStartTime { get; set; }
}

public partial class MessageDispatcher(ILogger _logger, Configuration _configuration, IDataService _dataService, ISoundFilter _soundFilter, IReportService _reportService, IPlaybackService _playbackService, IGameInteropService _gameInteropService, IFramework _framework, IClientState _clientState) : IMessageDispatcher
{
  private Dictionary<MessageSource, PlaybackQueue> _queues = [];
  private InterceptedSound? _interceptedSound;

  public bool Paused { get; set; } = false;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    foreach (MessageSource source in Enum.GetValues<MessageSource>())
      _queues[source] = new();

    _soundFilter.OnCutsceneAudioDetected += SoundFilter_OnCutSceneAudioDetected;

    _framework.Update += OnFrameworkUpdate;
    _playbackService.PlaybackStarted += OnPlaybackStarted;
    _playbackService.PlaybackCompleted += OnPlaybackCompleted;
    _playbackService.QueuedLineSkipped += OnQueuedLineSkipped;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _soundFilter.OnCutsceneAudioDetected -= SoundFilter_OnCutSceneAudioDetected;

    _framework.Update -= OnFrameworkUpdate;
    _playbackService.PlaybackStarted -= OnPlaybackStarted;
    _playbackService.PlaybackCompleted -= OnPlaybackCompleted;
    _playbackService.QueuedLineSkipped -= OnQueuedLineSkipped;

    _queues = [];

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public void ClearQueue()
  {
    foreach (PlaybackQueue playbackQueue in _queues.Values)
    {
      playbackQueue.Queue.Clear();
    }
  }

  private void OnFrameworkUpdate(IFramework framework)
  {
    int timeoutSec = (_configuration.ForceLocalGeneration || _configuration.EnableLocalGeneration) ? 45 : 3;

    foreach (PlaybackQueue playbackQueue in _queues.Values)
    {
      if (playbackQueue.PlaybackQueueState == PlaybackQueueState.AwaitingConfirmation)
      {
        if ((DateTime.UtcNow - playbackQueue.PlaybackStartTime) >= TimeSpan.FromSeconds(timeoutSec))
        {
          _logger.Debug($"Queue timed out. Setting playback state to stopped.");
          playbackQueue.PlaybackQueueState = PlaybackQueueState.Stopped;
        }
      }

      if (playbackQueue.PlaybackQueueState == PlaybackQueueState.Stopped && !playbackQueue.Queue.IsEmpty)
      {
        if (!Paused && playbackQueue.Queue.TryDequeue(out XivMessage? message))
        {
          _logger.Debug($"Playing queued message: {message.Id}");
          _playbackService.RemoveQueuedLine(message);
          playbackQueue.PlaybackStartTime = DateTime.UtcNow;
          playbackQueue.PlaybackQueueState = PlaybackQueueState.AwaitingConfirmation;
          _ = _playbackService.Play(message);
        }
      }
    }
  }

  private void OnPlaybackStarted(object? sender, XivMessage message)
  {
    _logger.Debug($"{message.Source} Playback Started.");
    _queues[message.Source].PlaybackQueueState = PlaybackQueueState.Playing;
  }

  private void OnPlaybackCompleted(object? sender, XivMessage message)
  {
    int count = _playbackService.CountPlaying(message.Source);
    if (count == 0)
    {
      _logger.Debug($"{message.Source} Playback Completed.");
      _queues[message.Source].PlaybackQueueState = PlaybackQueueState.Stopped;
    }
    else
    {
      _logger.Debug($"{message.Source} Playback Completed, but {count} are still playing.");
    }
  }

  private void OnQueuedLineSkipped(object? sender, XivMessage message)
  {
    XivMessage? itemToRemove = _queues[message.Source].Queue.FirstOrDefault(item => item.Id == message.Id);

    if (itemToRemove != null)
    {
      ConcurrentQueue<XivMessage> newQueue = new();

      foreach (XivMessage item in _queues[message.Source].Queue)
        if (item.Id != message.Id)
          newQueue.Enqueue(item);

      _queues[message.Source].Queue = newQueue;

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

  public async Task TryDispatch(MessageSource source, string rawSpeaker, string rawSentence, uint? speakerBaseId = null)
  {
    if (_dataService.Manifest == null) return;
    string speaker = rawSpeaker;
    string sentence = rawSentence;

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

      (mappedNpcFound, mappedNpc) = GetNpcFromMappings(SpeakerMappingType.Retainer, sentence);
      if (mappedNpcFound) _logger.Debug("Found mapped retainer npc");
      else _logger.Debug("Failed to find mapped retainer npc");
    }

    string? playerName = await _gameInteropService.RunOnFrameworkThread(() => _clientState.LocalPlayer?.Name.TextValue ?? null);
    bool sentenceHasName = SentenceHasPlayerName(sentence, playerName);

    // NOTE: This might want to support more than "???" speakers in the future.
    // For now though, all our nameless mappings are "???" speakers so this is fine.
    // Would have to map speaker+sentence if this is expanded.
    if (speaker == "???")
    {
      // Nameless mappings use a cleaned sentence with legacy name replacement.
      string sentenceToMatch = sentence;
      if (sentenceHasName) (_, sentenceToMatch) = CleanMessage(speaker, sentence, playerName, true, false);
      (mappedNpcFound, mappedNpc) = GetNpcFromMappings(SpeakerMappingType.Nameless, sentenceToMatch);
      if (mappedNpcFound) _logger.Debug("Found mapped nameless npc");
      else _logger.Debug("Failed to find mapped nameless npc");
    }

    if (mappedNpcFound && mappedNpc == null)
    {
      _logger.Debug("Mapped npc returned null. Skipping this voiceline");
      return;
    }

    // Clean message and replace names with new replacement method.
    (string cleanedSpeaker, string cleanedSentence) = CleanMessage(speaker, sentence, playerName, false, false);

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
      if (source != MessageSource.ChatMessage && voice != null && npc != null)
        npc.VoiceId = voice.Id;
    }

    // Cache player npc to assign a gender to chatmessage tts when they're not near you.
    if (source == MessageSource.ChatMessage && npc != null)
      _dataService.CachePlayer(rawSpeaker, npc);

    // Try to retrieve said cached npc if they're not near you.
    if (source == MessageSource.ChatMessage && npc == null)
      npc = _dataService.TryGetCachedPlayer(rawSpeaker);

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
      cleanedSpeaker,
      cleanedSentence,
      rawSpeaker,
      rawSentence,
      npc,
      voice,
      voicelinePath
    );

    _logger.Debug($"Constructed message: {message}");

    // If speaker is ignored, do not send a report, still let it be voiced by local TTS.
    bool isIgnoredSpeaker = _dataService.Manifest.IgnoredSpeakers.Contains(speaker);

    // Report if it's not a chat message, it couldn't find a voiceline and the speaker is not ignored.
    if (source != MessageSource.ChatMessage && message.VoicelinePath == null && !_dataService.Manifest.IgnoredSpeakers.Contains(speaker))
      _reportService.Report(message);

    bool allowed = true;
    bool queued = false;
    bool isNarrator = message.Speaker == "Narrator";
    switch (source)
    {
      case MessageSource.AddonTalk:
        allowed = _configuration.AddonTalkEnabled
          && (isNarrator
            ? _configuration.AddonTalkNarratorEnabled
            : !message.IsLocalTTS || _configuration.AddonTalkTTSEnabled);
        queued = _configuration.QueueDialogue;
        break;
      case MessageSource.AddonBattleTalk:
        allowed = _configuration.AddonBattleTalkEnabled
          && (isNarrator
            ? _configuration.AddonTalkNarratorEnabled
            : !message.IsLocalTTS || _configuration.AddonBattleTalkTTSEnabled);
        queued = true;
        break;
      case MessageSource.AddonMiniTalk:
        allowed = _configuration.AddonMiniTalkEnabled && (!message.IsLocalTTS || _configuration.AddonMiniTalkTTSEnabled);
        queued = true;
        break;
      case MessageSource.ChatMessage:
        queued = _configuration.QueueChatMessages;
        break;
    }

    if (source == MessageSource.AddonMiniTalk && _configuration.PrintBubbleMessages)
      _logger.Chat(message.RawSentence, "", "", npc?.Id ?? "Bubble", XivChatType.NPCDialogue, false);

    if (isNarrator && _configuration.PrintNarratorMessages)
      _logger.Chat(message.RawSentence, "", "", "Narrator", XivChatType.NPCDialogue, false);

    if (_configuration.MuteEnabled || !allowed || (isRetainer && !_configuration.RetainersEnabled) || (message.IsLocalTTS && !_configuration.LocalTTSEnabled))
    {
      _logger.Debug($"Not playing line due to user configuration. MuteEnabled:{_configuration.MuteEnabled} allowed:{allowed} isNarrator:{isNarrator} isRetainer:{isRetainer} isLocalTTS:{message.IsLocalTTS}");
      return;
    }

    if (queued)
    {
      _logger.Debug($"Queueing message: {message.Id}");
      _queues[message.Source].Queue.Enqueue(message);
      _playbackService.AddQueuedLine(message);
    }
    else
    {
      _logger.Debug($"Playing message: {message.Id}");
      _ = _playbackService.Play(message);
    }
  }
}
