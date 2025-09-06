namespace XivVoices.Services;

public enum PlaybackQueueState
{
  AwaitingConfirmation,
  Playing,
  Stopped
}

public interface IPlaybackQueueBase
{
  bool QueueEnabled { get; set; }

  void QueueStart();
  void QueueStop();

  void EnqueueMessage(string speaker, string sentence, uint? speakerBaseId = null);
}

public abstract class PlaybackQueue(MessageSource _messageSource, ILogger _logger, Configuration _configuration, IPlaybackService _playbackService, IMessageDispatcher _messageDispatcher, IFramework _framework)
{
  public bool QueueEnabled = true;

  private readonly Queue<(string speaker, string sentence, uint? speakerBaseId)> _queue = new();
  private PlaybackQueueState _playbackQueueState = PlaybackQueueState.Stopped;
  private DateTime _playbackStartTime;

  public void QueueStart()
  {
    _framework.Update += OnFrameworkUpdate;
    _playbackService.PlaybackStarted += OnPlaybackStarted;
    _playbackService.PlaybackCompleted += OnPlaybackCompleted;
    _playbackService.QueuedLineSkipped += OnQueuedLineSkipped;
  }

  public void QueueStop()
  {
    _framework.Update -= OnFrameworkUpdate;
    _playbackService.PlaybackStarted -= OnPlaybackStarted;
    _playbackService.PlaybackCompleted -= OnPlaybackCompleted;
    _playbackService.QueuedLineSkipped -= OnQueuedLineSkipped;
  }

  private void OnFrameworkUpdate(IFramework framework)
  {
    int timeoutSec = (_configuration.ForceLocalGeneration || _configuration.EnableLocalGeneration) ? 45 : 3;

    if (_playbackQueueState == PlaybackQueueState.AwaitingConfirmation)
      if ((DateTime.Now - _playbackStartTime) >= TimeSpan.FromSeconds(timeoutSec))
        _playbackQueueState = PlaybackQueueState.Stopped;

    if (_playbackQueueState == PlaybackQueueState.Stopped && _queue.Count > 0)
    {
      (string speaker, string sentence, uint? speakerBaseId) = _queue.Dequeue();
      _ = _messageDispatcher.TryDispatch(_messageSource, speaker, sentence, speakerBaseId);
      _playbackService.RemoveQueuedLine($"{speaker}+{sentence}");
      _playbackStartTime = DateTime.Now;
      _playbackQueueState = PlaybackQueueState.AwaitingConfirmation;
    }
  }

  private void OnPlaybackStarted(object? sender, XivMessage message)
  {
    if (message.Source != _messageSource) return;
    _logger.Debug($"{_messageSource} Playback Started.");
    _playbackQueueState = PlaybackQueueState.Playing;
  }

  private void OnPlaybackCompleted(object? sender, XivMessage message)
  {
    if (message.Source != _messageSource) return;
    _logger.Debug($"{_messageSource} Playback Completed.");
    _playbackQueueState = PlaybackQueueState.Stopped;
  }

  private void OnQueuedLineSkipped(object? sender, XivMessage message)
  {
    (string speaker, string sentence, uint? speakerBaseId) itemToRemove = _queue.FirstOrDefault(item => $"{item.speaker}+{item.sentence}" == message.Id);

    if (itemToRemove != default)
    {
      Queue<(string speaker, string sentence, uint? speakerBaseId)> newQueue = new(_queue.Where(item => item != itemToRemove));
      _queue.Clear();
      foreach ((string speaker, string sentence, uint? speakerBaseId) item in newQueue)
      {
        _queue.Enqueue(item);
      }

      _logger.Debug($"Removed queued line: {message.Id}");
    }
  }

  public void EnqueueMessage(string speaker, string sentence, uint? speakerBaseId = null)
  {
    if (QueueEnabled)
    {
      _playbackService.AddQueuedLine(new XivMessage($"{speaker}+{sentence}", _messageSource, null, speaker, sentence, speaker, sentence, null, null, true));
      _queue.Enqueue((speaker, sentence, speakerBaseId));
    }
    else
      _ = _messageDispatcher.TryDispatch(_messageSource, speaker, sentence, speakerBaseId);
  }
}
