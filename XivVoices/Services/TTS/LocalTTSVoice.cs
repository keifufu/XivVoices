namespace XivVoices.Services;

public interface ILocalTTSVoice : IDisposable
{
  IntPtr Pointer { get; }
  void AcquireReaderLock();
  void ReleaseReaderLock();
}

public class LocalTTSVoice : ILocalTTSVoice
{
  private const int Timeout = 8000;
  private readonly ReaderWriterLock _lock = new();
  private readonly string _voiceName;

  private readonly ILogger _logger;

  public IntPtr Pointer { get; private set; }
  public FixedPointerToHeapAllocatedMem ConfigPointer { get; private set; }
  public FixedPointerToHeapAllocatedMem ModelPointer { get; private set; }
  public bool Disposed { get; private set; }

  public LocalTTSVoice(
    string voiceName,
    string toolsDirectory,
    ILogger logger
  )
  {
    _voiceName = voiceName;
    _logger = logger;

    _logger.Debug($"[{_voiceName}] Loading voice");

    string modelPath = Path.Combine(toolsDirectory, $"{_voiceName}.bytes");
    string configPath = Path.Combine(toolsDirectory, $"{_voiceName}.config.json");

    if (!File.Exists(modelPath))
      throw new FileNotFoundException($"Missing voice model: {modelPath}");

    if (!File.Exists(configPath))
      throw new FileNotFoundException($"Missing config: {configPath}");

    byte[] modelBytes = File.ReadAllBytes(modelPath);
    byte[] configBytes = File.ReadAllBytes(configPath);

    _logger.Debug($"[{_voiceName}] Allocating voice config and model");
    ConfigPointer = FixedPointerToHeapAllocatedMem.Create(configBytes, (uint)configBytes.Length);
    ModelPointer = FixedPointerToHeapAllocatedMem.Create(modelBytes, (uint)modelBytes.Length);

    _logger.Debug($"[{_voiceName}] LocalTTSLoadVoice {ConfigPointer.Address} {ConfigPointer.SizeInBytes} {ModelPointer.Address} {ModelPointer.SizeInBytes}");
    Pointer = LocalTTSEngine.LocalTTSLoadVoice(
      ConfigPointer.Address, ConfigPointer.SizeInBytes,
      ModelPointer.Address, ModelPointer.SizeInBytes
    );

    _logger.Debug($"[{_voiceName}] LocalTTSSetSpeakerId {Pointer} 0");
    LocalTTSEngine.LocalTTSSetSpeakerId(Pointer, 0);

    _logger.Debug($"[{_voiceName}] Voice loaded successfully.");
  }

  public void AcquireReaderLock() =>
    _lock.AcquireReaderLock(Timeout);

  public void ReleaseReaderLock() =>
    _lock.ReleaseReaderLock();

  public void Dispose()
  {
    _lock.AcquireWriterLock(Timeout);
    try
    {
      if (Disposed) return;
      Disposed = true;

      _logger.Debug($"[{_voiceName}] ConfigPointer.Free {ConfigPointer}");
      ConfigPointer.Free();

      _logger.Debug($"[{_voiceName}] ModelPointer.Free {ModelPointer}");
      ModelPointer.Free();

      _logger.Debug($"[{_voiceName}] LocalTTSFreeVoice {Pointer}");
      LocalTTSEngine.LocalTTSFreeVoice(Pointer);

      _logger.Debug($"[{_voiceName}] Voice resources disposed.");
    }
    finally
    {
      _lock.ReleaseWriterLock();
    }
  }
}

public class FixedPointerToHeapAllocatedMem : IDisposable
{
  private GCHandle _handle;
  public IntPtr Address { get; private set; }

  public static FixedPointerToHeapAllocatedMem Create<T>(T Object, uint SizeInBytes)
  {
    FixedPointerToHeapAllocatedMem pointer = new()
    {
      _handle = GCHandle.Alloc(Object, GCHandleType.Pinned),
      SizeInBytes = SizeInBytes
    };
    pointer.Address = pointer._handle.AddrOfPinnedObject();
    return pointer;
  }

  public bool IsValid => _handle.IsAllocated && Address != IntPtr.Zero;

  public void Free()
  {
    if (_handle.IsAllocated)
    {
      _handle.Free();
      Address = IntPtr.Zero;
    }
  }

  public void Dispose() => Free();

  public uint SizeInBytes { get; private set; }
}
