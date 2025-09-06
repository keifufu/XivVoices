using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace XivVoices.Services;

public interface IAddonMiniTalkProvider : IHostedService;

public class AddonMiniTalkProvider(ILogger _logger, Configuration _configuration, IGameInteropService _gameInteropService, IPlaybackService _playbackService, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IGameInteropProvider _gameInteropProvider, IFramework _framework) : PlaybackQueue(MessageSource.AddonMiniTalk, _logger, _configuration, _playbackService, _messageDispatcher, _framework), IAddonMiniTalkProvider
{
  private readonly ConcurrentDictionary<string, DateTime> _recentSentences = new();
  private readonly TimeSpan _sentenceCooldown = TimeSpan.FromSeconds(30);

  public Task StartAsync(CancellationToken cancellationToken)
  {
    QueueStart();

    _gameInteropProvider.InitializeFromAttributes(this);
    _openBubbleHook.Enable();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    QueueStop();

    _openBubbleHook?.Dispose();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private unsafe delegate void* OpenBubbleDelegate(nint self, GameObject* actor, nint textPtr, bool notSure, int attachmentPointID);
  [Signature("E8 ?? ?? ?? ?? F6 86 ?? ?? ?? ?? ?? C7 46 ?? ?? ?? ?? ??", DetourName = nameof(OpenBubbleDetour))]
  private readonly Hook<OpenBubbleDelegate> _openBubbleHook = null!;
  private unsafe void* OpenBubbleDetour(nint self, GameObject* actor, nint textPtr, bool notSure, int attachmentPointID)
  {
    if (actor != null && (byte)actor->ObjectKind != (byte)Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player && !_gameInteropService.IsInCutscene())
    {
      string speaker = actor->GetName().ToString();
      if (string.IsNullOrEmpty(speaker)) speaker = "Bubble";
      string sentence = MemoryHelper.ReadSeStringNullTerminated(textPtr).ToString();

      if (_selfTestService.Step == SelfTestStep.Provider_MiniTalk)
        _selfTestService.Report_Provider_MiniTalk(actor, sentence);

      DateTime now = DateTime.UtcNow;
      if (_recentSentences.TryGetValue(sentence, out DateTime lastTime) && now - lastTime < _sentenceCooldown)
      {
        _logger.Debug($"Skipping duplicate AddonMiniTalk line: {sentence} (on cooldown)");
      }
      else
      {
        _recentSentences[sentence] = now;
        _logger.Debug($"speaker::{speaker} sentence::{sentence}");
        EnqueueMessage(speaker, sentence, actor->BaseId);
      }
    }

    return _openBubbleHook.Original(self, actor, textPtr, notSure, attachmentPointID);
  }
}
