using FFXIVClientStructs.FFXIV.Client.Sound;
using InteropGenerator.Runtime;

namespace XivVoices.Services;

public interface ISoundFilter : IHostedService
{
  event EventHandler<InterceptedSoundEventArgs>? OnVoicelineDetected;
}

public class SoundFilter(
  ILogger _logger,
  Configuration _configuration,
  ISelfTestService _selfTestService,
  IGameInteropProvider _interopProvider,
  IGameInteropService _gameInteropService) : ISoundFilter
{
  private Hook<SoundManager.Delegates.PlaySound>? _playSoundHook;
  private Hook<SoundManager.Delegates.PlayCutsceneVoSound>? _playCutsceneVoSoundHook;

  public event EventHandler<InterceptedSoundEventArgs>? OnVoicelineDetected;

  public unsafe Task StartAsync(CancellationToken cancellationToken)
  {
#if !NO_HOOKS
    _playSoundHook = _interopProvider.HookFromAddress<SoundManager.Delegates.PlaySound>(
      SoundManager.Addresses.PlaySound.Value,
      PlaySoundDetour
    );
    _playSoundHook.Enable();

    _playCutsceneVoSoundHook = _interopProvider.HookFromAddress<SoundManager.Delegates.PlayCutsceneVoSound>(
      SoundManager.Addresses.PlayCutsceneVoSound.Value,
      PlayCutsceneVoSoundDetour
    );
    _playCutsceneVoSoundHook.Enable();
#endif

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _playSoundHook?.Dispose();
    _playCutsceneVoSoundHook?.Dispose();

    return _logger.ServiceLifecycle();
  }

  // AddonMiniTalk, AddonBattleTalk
  private unsafe SoundData* PlaySoundDetour(
    SoundManager* thisPtr,
    CStringPointer path,
    float volume,
    uint fadeInDuration,
    float posX,
    float posY,
    float posZ,
    float speed,
    int a9,
    uint soundNumber,
    bool autoRelease,
    SoundVolumeCategory volumeCategory,
    bool a13,
    int midiNote,
    bool a15,
    bool defaultFadeOut,
    bool isPositional,
    bool a18)
  {
    SoundData* soundData = _playSoundHook!.OriginalDisposeSafe(
      thisPtr, path, volume, fadeInDuration, posX, posY, posZ, speed, a9, soundNumber, autoRelease,
      volumeCategory, a13, midiNote, a15, defaultFadeOut, isPositional, a18);
    if (soundData == null) return soundData;

    string lPath = $"{path.ToString().ToLower(CultureInfo.InvariantCulture)}/{soundData->SoundNumber}";

    if (_selfTestService.Step == SelfTestStep.SoundFilter_PlaySound)
      _selfTestService.Report_SoundFilter_PlaySound(lPath);

    if (ShouldFilter(lPath)) soundData->Volume = 0.0f;
    return soundData;
  }

  // AddonTalk
  private unsafe SoundData* PlayCutsceneVoSoundDetour(SoundManager* thisPtr, CStringPointer path)
  {
    SoundData* soundData = _playCutsceneVoSoundHook!.OriginalDisposeSafe(thisPtr, path);
    if (soundData == null) return soundData;

    string lPath = $"{path.ToString().ToLower(CultureInfo.InvariantCulture)}/{soundData->SoundNumber}";

    if (_selfTestService.Step == SelfTestStep.SoundFilter_PlayCutsceneVOSound)
      _selfTestService.Report_SoundFilter_PlayCutsceneVOSound(lPath);

    if (ShouldFilter(lPath) || _selfTestService.Step == SelfTestStep.SoundFilter_PlayCutsceneVOSound) soundData->Volume = 0.0f;
    return soundData;
  }

  private bool ShouldFilter(string path)
  {
    // if (!path.Contains("sound/battle") && !path.Contains("sound/system") && !path.Contains("sound/foot") && !path.Contains("sound/vfx") && !path.Contains("bgcommon/sound")) _logger.Debug(path);

    // All lines we care about seem to be SoundNumber 0
    if (!path.EndsWith("/0", StringComparison.Ordinal)) return false;

    // MiniTalk and Battletalk, sound/voice/vo_line/8202105_en.scd/0
    if (path.StartsWith("sound/voice/vo_line/", StringComparison.Ordinal))
    {
      OnVoicelineDetected?.Invoke(this, new InterceptedSoundEventArgs(true, path));
      return false;
    }

    // AddonTalk
    // ARR cut/ffxiv/sound/manfst/manfst304/vo_manfst304_100010_m_en.scd/0
    // POST-ARR cut/ffxiv/sound/voicem/voiceman_02500/vo_voiceman_02500_w00010_m_en.scd/0
    if (path.StartsWith("cut/ffxiv", StringComparison.Ordinal) && (path.Contains("vo_man") || path.Contains("vo_voiceman")))
    {
      bool shouldBlock = _configuration.ReplaceVoicedARRCutscenes && _gameInteropService.IsInCutscene() && !_configuration.MuteEnabled;
      if (shouldBlock) _logger.Debug($"Blocked ARR voiceline: {path}");
      OnVoicelineDetected?.Invoke(this, new InterceptedSoundEventArgs(!shouldBlock, path));
      return shouldBlock;
    }

    // AddonTalk
    // HW cut/ex1/sound/voicem/voiceman_03001/vo_voiceman_03001_000010_m_en.scd/0
    // EW cut/ex4/sound/voicem/voiceman_06006/vo_voiceman_06006_006910_m_en.scd/0
    if (path.StartsWith("cut/ex", StringComparison.Ordinal) && path.Contains("sound/voicem/voiceman"))
    {
      OnVoicelineDetected?.Invoke(this, new InterceptedSoundEventArgs(true, path));
      return false;
    }

    return false;
  }
}

public class InterceptedSoundEventArgs(bool shouldBlock, string soundPath) : EventArgs
{
  public DateTime CreationDate { get; init; } = DateTime.UtcNow;

  public bool ShouldBlock { get; set; } = shouldBlock;
  public string SoundPath { get; set; } = soundPath;

  public bool IsValid() => (DateTime.UtcNow - CreationDate).TotalSeconds <= 1;
}
