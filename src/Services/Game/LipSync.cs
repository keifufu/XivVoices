namespace XivVoices.Services;

public interface ILipSync
{
  void TryLipSync(XivMessage message, double durationSeconds);
  void TryStopLipSync(XivMessage message);
}

public partial class LipSync(ILogger _logger, IGameInteropService _gameInteropService, IFramework _framework) : ILipSync
{
  // ActionTimeline exd sheet
  private const ushort SpeakNone = 0;
  private const ushort SpeakNormalLong = 631;
  private const ushort SpeakNormalMiddle = 630;
  private const ushort SpeakNormalShort = 629;

  private Dictionary<string, CancellationTokenSource> _runningTasks = [];

  private string GetTaskId(XivMessage message)
  {
    // Using .Speaker instead of .Id now as we don't want to
    // lipsync the same character multiple times at once.
    if (!string.IsNullOrEmpty(message.Speaker)) return message.Speaker;
    if (message.NpcData != null) return message.NpcData.BaseId.ToString();
    return message.Id;
  }

  public async void TryLipSync(XivMessage message, double durationSeconds)
  {
    if (durationSeconds < 0.2f) return;

    IntPtr character = await _gameInteropService.TryFindCharacter(message.Speaker, message.NpcData?.BaseId ?? 0);
    if (character == IntPtr.Zero)
    {
      _logger.Debug($"No lipsync target found for speaker {message.Speaker}");
      return;
    }

    Dictionary<int, int> mouthMovement = [];
    int durationMs = (int)(durationSeconds * 1000);
    int durationRounded = (int)Math.Floor(durationSeconds);
    int remaining = durationRounded;
    mouthMovement[6] = remaining / 4;
    remaining %= 4;
    mouthMovement[5] = remaining / 2;
    remaining %= 2;
    mouthMovement[4] = remaining / 1;
    remaining %= 1;

    _logger.Debug($"durationMs[{durationMs}] durationRounded[{durationRounded}] fours[{mouthMovement[6]}] twos[{mouthMovement[5]}] ones[{mouthMovement[4]}]");

    // Decide on the mode
    CharacterMode initialCharacterMode = TryGetCharacterMode(character);
    CharacterMode characterMode = CharacterMode.EmoteLoop;

    if (!_runningTasks.ContainsKey(GetTaskId(message)))
    {
      CancellationTokenSource cts = new();
      _runningTasks.Add(GetTaskId(message), cts);
      var token = cts.Token;

      Task task = Task.Run(async () =>
      {
        try
        {
          await Task.Delay(100, token);

          // 4-Second Lips Movement Animation
          if (!token.IsCancellationRequested && mouthMovement[6] > 0 && character != IntPtr.Zero)
          {
            await _framework.RunOnFrameworkThread(() =>
            {
              TrySetCharacterMode(character, characterMode);
              TrySetLipsOverride(character, SpeakNormalLong);
            });

            int adjustedDelay = CalculateAdjustedDelay(mouthMovement[6] * 4000, 6);
            _logger.Debug($"Task was started mouthMovement[6] durationMs[{mouthMovement[6] * 4}] delay [{adjustedDelay}]");

            await Task.Delay(adjustedDelay, token);

            if (!token.IsCancellationRequested && character != IntPtr.Zero)
            {
              _logger.Debug("Task mouthMovement[6] has finished");
              await _framework.RunOnFrameworkThread(() =>
              {
                TrySetCharacterMode(character, initialCharacterMode);
                TrySetLipsOverride(character, SpeakNone);
              });
            }
          }

          // 2-Second Lips Movement Animation
          if (!token.IsCancellationRequested && mouthMovement[5] > 0 && character != IntPtr.Zero)
          {
            await _framework.RunOnFrameworkThread(() =>
            {
              TrySetCharacterMode(character, characterMode);
              TrySetLipsOverride(character, SpeakNormalMiddle);
            });

            int adjustedDelay = CalculateAdjustedDelay(mouthMovement[5] * 2000, 5);
            _logger.Debug($"Task was started mouthMovement[5] durationMs[{mouthMovement[5] * 2}] delay [{adjustedDelay}]");

            await Task.Delay(adjustedDelay, token);

            if (!token.IsCancellationRequested && character != IntPtr.Zero)
            {
              _logger.Debug("Task mouthMovement[5] has finished");
              await _framework.RunOnFrameworkThread(() =>
              {
                TrySetCharacterMode(character, initialCharacterMode);
                TrySetLipsOverride(character, SpeakNone);
              });
            }
          }

          // 1-Second Lips Movement Animation
          if (!token.IsCancellationRequested && mouthMovement[4] > 0 && character != IntPtr.Zero)
          {
            await _framework.RunOnFrameworkThread(() =>
            {
              TrySetCharacterMode(character, characterMode);
              TrySetLipsOverride(character, SpeakNormalShort);
            });

            int adjustedDelay = CalculateAdjustedDelay(mouthMovement[4] * 1000, 5);
            _logger.Debug($"Task was started mouthMovement[4] durationMs[{mouthMovement[4] * 1}] delay [{adjustedDelay}]");

            await Task.Delay(adjustedDelay, token);

            if (!token.IsCancellationRequested && character != IntPtr.Zero)
            {
              _logger.Debug("Task mouthMovement[4] has finished");
              await _framework.RunOnFrameworkThread(() =>
              {
                TrySetCharacterMode(character, initialCharacterMode);
                TrySetLipsOverride(character, SpeakNone);
              });
            }
          }

          _logger.Debug("LipSync was completed");
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
          TrySetCharacterMode(character, initialCharacterMode);
          TrySetLipsOverride(character, SpeakNone);

          cts.Dispose();
          if (_runningTasks.ContainsKey(GetTaskId(message)))
            _runningTasks.Remove(GetTaskId(message));
        }
      }, token);
    }
  }

  public void TryStopLipSync(XivMessage message)
  {
    if (_runningTasks.TryGetValue(GetTaskId(message), out var cts))
    {
      try
      {
        _logger.Debug("StopLipSync cancelling cts");
        cts.Cancel();
      }
      catch (Exception ex)
      {
        _logger.Error(ex);
      }
    }
  }

  int CalculateAdjustedDelay(int durationMs, int lipSyncType)
  {
    int delay = 0;
    int animationLoop;
    if (lipSyncType == 4)
      animationLoop = 1000;
    else if (lipSyncType == 5)
      animationLoop = 2000;
    else
      animationLoop = 4000;
    int halfStep = animationLoop / 2;

    if (durationMs <= (1 * animationLoop) + halfStep)
    {
      return (1 * animationLoop) - 50;
    }
    else
    {
      for (int i = 2; delay < durationMs; i++)
      {
        if (durationMs > (i * animationLoop) - halfStep && durationMs <= (i * animationLoop) + halfStep)
        {
          delay = (i * animationLoop) - 50;
          return delay;
        }
      }
    }

    return 404;
  }
}
