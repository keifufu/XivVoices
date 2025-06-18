using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace XivVoices.Services;

public interface ISoundFilter : IHostedService
{
  event EventHandler<InterceptedSound>? OnCutsceneAudioDetected;
}

// Cheers to 'SoundFilter' plugin.
// This intercepts all sounds that are loaded and played,
// allowing us to block XIVV's voices if a line is voiced,
// and to block ARR's in-game voices.
public class SoundFilter(ILogger _logger, Configuration _configuration, IGameInteropProvider _interopProvider) : ISoundFilter
{
  private const int ResourceDataPointerOffset = 0xB0;
  private readonly ConcurrentDictionary<IntPtr, string> _scds = new();

  private IntPtr _noSoundPtr;
  private IntPtr _infoPtr;

  public event EventHandler<InterceptedSound>? OnCutsceneAudioDetected;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    (nint noSoundPtr, nint infoPtr) = SetUpNoSound();
    _noSoundPtr = noSoundPtr;
    _infoPtr = infoPtr;

    _interopProvider.InitializeFromAttributes(this);
    _getResourceSyncHook.Enable();
    _getResourceAsyncHook.Enable();
    _loadSoundFileHook.Enable();
    _playSpecificSoundHook.Enable();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _getResourceSyncHook?.Dispose();
    _getResourceAsyncHook?.Dispose();
    _loadSoundFileHook?.Dispose();
    _playSpecificSoundHook?.Dispose();

    Marshal.FreeHGlobal(_infoPtr);
    Marshal.FreeHGlobal(_noSoundPtr);

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private byte[] GetNoSoundScd()
  {
    Assembly assembly = Assembly.GetExecutingAssembly();
    string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("nosound.scd"));
    Stream noSound = assembly.GetManifestResourceStream(resourceName)!;
    using MemoryStream memoryStream = new();
    noSound.CopyTo(memoryStream);
    return memoryStream.ToArray();
  }

  private (IntPtr noSoundPtr, IntPtr infoPtr) SetUpNoSound()
  {
    // get the data of an empty scd
    byte[] noSound = GetNoSoundScd();

    // allocate unmanaged memory for this data and copy the data into the memory
    nint noSoundPtr = Marshal.AllocHGlobal(noSound.Length);
    Marshal.Copy(noSound, 0, noSoundPtr, noSound.Length);

    // allocate some memory for feeding into the play sound function
    nint infoPtr = Marshal.AllocHGlobal(256);
    // write a pointer to the empty scd
    Marshal.WriteIntPtr(infoPtr + 8, noSoundPtr);
    // specify where the game should offset from for the sound index
    Marshal.WriteInt32(infoPtr + 0x88, 0x54);
    // specify the number of sounds in the file
    Marshal.WriteInt16(infoPtr + 0x94, 0);

    return (noSoundPtr, infoPtr);
  }

  private unsafe delegate void* GetResourceSyncDelegate(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown);
  [Signature("E8 ?? ?? ?? ?? 48 8B D8 8B C7", DetourName = nameof(GetResourceSyncDetour))]
  private readonly Hook<GetResourceSyncDelegate> _getResourceSyncHook = null!;
  private unsafe void* GetResourceSyncDetour(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown)
  {
    void* ret = _getResourceSyncHook.Original(pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown);
    GetResourceDetourInner(ret, pPath);
    return ret;
  }

  private unsafe delegate void* GetResourceAsyncDelegate(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown);
  [Signature("E8 ?? ?? ?? ?? 48 8B D8 EB 07 F0 FF 83", DetourName = nameof(GetResourceAsyncDetour))]
  private readonly Hook<GetResourceAsyncDelegate> _getResourceAsyncHook = null!;
  private unsafe void* GetResourceAsyncDetour(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown)
  {
    void* ret = _getResourceAsyncHook.Original(pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);
    GetResourceDetourInner(ret, pPath);
    return ret;
  }

  private unsafe void GetResourceDetourInner(void* ret, char* pPath)
  {
    if (ret != null && EndsWithDotScd((byte*)pPath))
    {
      nint scdData = Marshal.ReadIntPtr((IntPtr)ret + ResourceDataPointerOffset);
      // if we immediately have the scd data, cache it, otherwise add it to a waiting list to hopefully be picked up at sound play time
      if (scdData != IntPtr.Zero)
        _scds[scdData] = ReadTerminatedString((byte*)pPath);
    }
  }

  private unsafe delegate IntPtr LoadSoundFileDelegate(IntPtr resourceHandle, uint a2);
  [Signature("E8 ?? ?? ?? ?? 48 85 C0 75 12 B0 F6", DetourName = nameof(LoadSoundFileDetour))]
  private readonly Hook<LoadSoundFileDelegate> _loadSoundFileHook = null!;
  private unsafe IntPtr LoadSoundFileDetour(IntPtr resourceHandle, uint a2)
  {
    nint ret = _loadSoundFileHook.Original(resourceHandle, a2);
    try
    {
      ResourceHandle* handle = (ResourceHandle*)resourceHandle;
      string name = handle->FileName.ToString();
      if (name.EndsWith(".scd"))
      {
        nint dataPtr = Marshal.ReadIntPtr(resourceHandle + ResourceDataPointerOffset);
        _scds[dataPtr] = name;
      }
    }
    catch (Exception ex)
    {
      _logger.Error(ex.ToString());
    }

    return ret;
  }

  private unsafe delegate void* PlaySpecificSoundDelegate(long a1, int idx);
  [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F", DetourName = nameof(PlaySpecificSoundDetour))]
  private readonly Hook<PlaySpecificSoundDelegate> _playSpecificSoundHook = null!;
  private unsafe void* PlaySpecificSoundDetour(long a1, int idx)
  {
    try
    {
      bool shouldFilter = PlaySpecificSoundDetourInner(a1, idx);
      if (shouldFilter)
      {
        a1 = _infoPtr;
        idx = 0;
      }
    }
    catch (Exception ex)
    {
      _logger.Error(ex.ToString());
    }

    return _playSpecificSoundHook.Original(a1, idx);
  }

  private unsafe bool PlaySpecificSoundDetourInner(long a1, int idx)
  {
    if (a1 == 0) return false;

    byte* scdData = *(byte**)(a1 + 8);
    if (scdData == null) return false;

    if (!_scds.TryGetValue((IntPtr)scdData, out string? path)) return false;

    path = path.ToLowerInvariant();
    string specificPath = $"{path}/{idx}";

    return ShouldFilter(specificPath);
  }

  private bool ShouldFilter(string path)
  {
    if (path.Contains("vo_voiceman") || path.Contains("vo_man") || path.Contains("vo_line") || path.Contains("cut/ffxiv/"))
    {
      if ((path.Contains("vo_man") || (path.Contains("cut/ffxiv/") && path.Contains("vo_voiceman"))) && _configuration.ReplaceVoicedARRCutscenes)
      {
        OnCutsceneAudioDetected?.Invoke(this, new InterceptedSound(false, path));
        _logger.Debug("Blocking voiced ARR line in favor of XIVV");
        return true;
      }
      else
      {
        OnCutsceneAudioDetected?.Invoke(this, new InterceptedSound(true, path));
      }
    }

    return false;
  }

  private unsafe bool EndsWithDotScd(byte* pPath)
  {
    if (pPath == null) return false;

    int len = 0;
    while (pPath[len] != 0) len++;

    if (len < 4) return false;

    return pPath[len - 4] == (byte)'.' &&
      pPath[len - 3] == (byte)'s' &&
      pPath[len - 2] == (byte)'c' &&
      pPath[len - 1] == (byte)'d';
  }

  private unsafe byte[] ReadTerminatedBytes(byte* ptr)
  {
    if (ptr == null)
    {
      return [];
    }

    List<byte> bytes = [];
    while (*ptr != 0)
    {
      bytes.Add(*ptr);
      ptr += 1;
    }

    return [.. bytes];
  }

  private unsafe string ReadTerminatedString(byte* ptr)
  {
    return Encoding.UTF8.GetString(ReadTerminatedBytes(ptr));
  }
}

public class InterceptedSound(bool shouldBlock, string soundPath) : EventArgs
{
  public readonly DateTime CreationDate = DateTime.UtcNow;

  public bool ShouldBlock { get; set; } = shouldBlock;
  public string SoundPath { get; set; } = soundPath;

  public bool IsValid() => (DateTime.UtcNow - CreationDate).TotalSeconds <= 3;
}
