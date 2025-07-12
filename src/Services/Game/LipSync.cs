namespace XivVoices.Services;

public interface ILipSync
{
  Task TryLipSync(XivMessage message, double durationSeconds);
  void TryStopLipSync(XivMessage message);
}

public partial class LipSync(ILogger _logger, IGameInteropService _gameInteropService, IFramework _framework) : ILipSync
{
  // ActionTimeline exd sheet
  private const ushort SpeakNone = 0;
  private const ushort SpeakNormalLong = 631;
  private const ushort SpeakNormalMiddle = 630;
  private const ushort SpeakNormalShort = 629;

  private ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = [];

  private string GetTaskId(XivMessage message)
  {
    // Using .Speaker instead of .Id now as we don't want to
    // lipsync the same character multiple times at once.
    if (!string.IsNullOrEmpty(message.Speaker)) return message.Speaker;
    if (message.Npc != null) return message.Npc.BaseId.ToString();
    return message.Id;
  }

  public async Task TryLipSync(XivMessage message, double durationSeconds)
  {
    if (durationSeconds < 0.2f) return;

    TryStopLipSync(message);

    IntPtr character = await _gameInteropService.TryFindCharacter(message.Speaker, message.Npc?.BaseId ?? 0);
    if (character == IntPtr.Zero)
    {
      _logger.Debug($"No lipsync target found for speaker {message.Speaker} ({message.Npc?.BaseId})");
      return;
    }

    CancellationTokenSource cts = new();
    string taskId = GetTaskId(message);
    if (!_runningTasks.TryAdd(taskId, cts))
    {
      cts.Dispose();
      _logger.Debug($"Could not add CTS for {taskId}, task already running.");
      return;
    }

    CancellationToken token = cts.Token;
    CharacterMode initialCharacterMode = TryGetCharacterMode(character);
    CharacterMode characterMode = CharacterMode.EmoteLoop;

    int durationMs = (int)(durationSeconds * 1000);
    int durationRounded = (int)Math.Floor(durationSeconds);
    int remaining = durationRounded;
    Dictionary<int, int> mouthMovement = new()
    {
      [6] = durationRounded / 4,
      [5] = durationRounded % 4 / 2,
      [4] = durationRounded % 2
    };

    _logger.Debug($"durationMs[{durationMs}] durationRounded[{durationRounded}] fours[{mouthMovement[6]}] twos[{mouthMovement[5]}] ones[{mouthMovement[4]}]");

    await Task.Run(async () =>
    {
      try
      {
        await Task.Delay(100, token);

        if (mouthMovement[6] > 0)
        {
          int delay = CalculateAdjustedDelay(mouthMovement[6] * 4000, 6);
          _logger.Debug($"Starting 4s lip movement. Delay: {delay}");
          await AnimateLipSync(character, initialCharacterMode, characterMode, SpeakNormalLong, delay, token);
        }

        if (mouthMovement[5] > 0)
        {
          int delay = CalculateAdjustedDelay(mouthMovement[5] * 2000, 5);
          _logger.Debug($"Starting 2s lip movement. Delay: {delay}");
          await AnimateLipSync(character, initialCharacterMode, characterMode, SpeakNormalMiddle, delay, token);
        }

        if (mouthMovement[4] > 0)
        {
          int delay = CalculateAdjustedDelay(mouthMovement[4] * 1000, 4);
          _logger.Debug($"Starting 1s lip movement. Delay: {delay}");
          await AnimateLipSync(character, initialCharacterMode, characterMode, SpeakNormalShort, delay, token);
        }

        _logger.Debug("LipSync completed successfully");
      }
      catch (TaskCanceledException)
      {
        _logger.Debug("LipSync was cancelled");
      }
      catch (Exception ex)
      {
        _logger.Error(ex);
      }
      finally
      {
        await _framework.RunOnFrameworkThread(() =>
        {
          TrySetCharacterMode(character, initialCharacterMode);
          TrySetLipsOverride(character, SpeakNone);
        });

        if (_runningTasks.TryRemove(taskId, out CancellationTokenSource? oldCts))
          oldCts.Dispose();
      }
    }, token);
  }

  public void TryStopLipSync(XivMessage message)
  {
    string taskId = GetTaskId(message);
    if (_runningTasks.TryRemove(taskId, out CancellationTokenSource? cts))
    {
      try
      {
        _logger.Debug($"StopLipSync cancelling CTS for {taskId}");
        cts.Cancel();
        cts.Dispose();
      }
      catch (Exception ex)
      {
        _logger.Error(ex);
      }
    }
  }

  private async Task AnimateLipSync(IntPtr character, CharacterMode initialMode, CharacterMode targetMode, ushort speakValue, int delayMs, CancellationToken token)
  {
    if (token.IsCancellationRequested || character == IntPtr.Zero) return;

    await _framework.RunOnFrameworkThread(() =>
    {
      TrySetCharacterMode(character, targetMode);
      TrySetLipsOverride(character, speakValue);
    });

    await Task.Delay(delayMs, token);

    if (!token.IsCancellationRequested && character != IntPtr.Zero)
    {
      await _framework.RunOnFrameworkThread(() =>
      {
        TrySetCharacterMode(character, initialMode);
        TrySetLipsOverride(character, SpeakNone);
      });
      _logger.Debug($"LipSync {speakValue} block finished after {delayMs}ms");
    }
  }

  private int CalculateAdjustedDelay(int durationMs, int lipSyncType)
  {
    int animationLoop = lipSyncType switch
    {
      4 => 1000,
      5 => 2000,
      6 => 4000,
      _ => 4000
    };

    int halfStep = animationLoop / 2;

    for (int i = 1; i <= 10; i++)
    {
      int ideal = i * animationLoop;
      if (durationMs <= ideal + halfStep)
        return ideal - 50;
    }

    _logger.Debug($"CalculateAdjustedDelay fell through: {durationMs}, {lipSyncType}");
    return 404;
  }
}
