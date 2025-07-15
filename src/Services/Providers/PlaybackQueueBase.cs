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

public abstract class PlaybackQueue(MessageSource _messageSource, ILogger _logger, IPlaybackService _playbackService, IMessageDispatcher _messageDispatcher, IFramework _framework)
{
  public bool QueueEnabled = true;

  private Queue<(string speaker, string sentence, uint? speakerBaseId)> _queue = new();
  private PlaybackQueueState _playbackQueueState = PlaybackQueueState.Stopped;
  private DateTime _playbackStartTime;

  public void QueueStart()
  {
    _framework.Update += OnFrameworkUpdate;
    _playbackService.PlaybackStarted += OnPlaybackStarted;
    _playbackService.PlaybackCompleted += OnPlaybackCompleted;
  }

  public void QueueStop()
  {
    _framework.Update -= OnFrameworkUpdate;
    _playbackService.PlaybackStarted -= OnPlaybackStarted;
    _playbackService.PlaybackCompleted -= OnPlaybackCompleted;
  }

  private void OnFrameworkUpdate(IFramework framework)
  {
    if (_playbackQueueState == PlaybackQueueState.AwaitingConfirmation)
      if ((DateTime.Now - _playbackStartTime) >= TimeSpan.FromSeconds(1))
        _playbackQueueState = PlaybackQueueState.Stopped;

    if (_playbackQueueState == PlaybackQueueState.Stopped && _queue.Count > 0)
    {
      (string speaker, string sentence, uint? speakerBaseId) = _queue.Dequeue();
      _ = _messageDispatcher.TryDispatch(_messageSource, speaker, sentence, speakerBaseId);
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

  public void EnqueueMessage(string speaker, string sentence, uint? speakerBaseId = null)
  {
    if (QueueEnabled)
      _queue.Enqueue((speaker, sentence, speakerBaseId));
    else
      _ = _messageDispatcher.TryDispatch(_messageSource, speaker, sentence, speakerBaseId);
  }
}
