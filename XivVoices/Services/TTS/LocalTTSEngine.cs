namespace XivVoices.Services;

public partial class LocalTTSEngine : IDisposable
{
  private readonly ILogger _logger;
  private readonly Configuration _configuration;

  private IntPtr _context;
  private bool _disposed = false;
  private readonly Lock _lock = new();

  public LocalTTSVoice?[] Voices { get; private set; } = new LocalTTSVoice?[2];

  public LocalTTSEngine(string toolsDirectory, ILogger logger, Configuration configuration)
  {
    lock (_lock)
    {
      _logger = logger;
      _configuration = configuration;

      string dllPath = Path.Join(toolsDirectory, "localtts.dll");
      string runtimeDllPath = Path.Join(toolsDirectory, "localttsruntime.dll");

      if (!File.Exists(dllPath) || !File.Exists(runtimeDllPath))
        throw new Exception("Failed to locate localtts.dll or localttsruntime.dll");

      string maleVoiceConfigPath = Path.Join(toolsDirectory, _configuration.LocalTTSVoiceMale + ".config.json");
      string maleVoiceModelPath = Path.Join(toolsDirectory, _configuration.LocalTTSVoiceMale + ".bytes");

      if (!File.Exists(maleVoiceConfigPath) || !File.Exists(maleVoiceModelPath))
        throw new Exception($"Failed to locate config or model for voice: {_configuration.LocalTTSVoiceMale}");

      string femaleVoiceConfigPath = Path.Join(toolsDirectory, _configuration.LocalTTSVoiceFemale + ".config.json");
      string femaleVoiceModelPath = Path.Join(toolsDirectory, _configuration.LocalTTSVoiceFemale + ".bytes");

      if (!File.Exists(femaleVoiceConfigPath) || !File.Exists(femaleVoiceModelPath))
        throw new Exception($"Failed to locate config or model for voice: {_configuration.LocalTTSVoiceFemale}");

      SetDefaultDllDirectories(0x00001000);
      AddDllDirectory(toolsDirectory);

      Voices[0] = new(_configuration.LocalTTSVoiceMale, toolsDirectory, _logger);
      Voices[1] = new(_configuration.LocalTTSVoiceFemale, toolsDirectory, _logger);

      _context = LocalTTSStart();

      _logger.Debug("LocalTTSEngine loaded");
    }
  }

  public void Dispose()
  {
    lock (_lock)
    {
      _disposed = true;

      if (_context != IntPtr.Zero)
      {
        LocalTTSFree(_context);
        _context = IntPtr.Zero;
      }

      if (Voices[0] != null)
      {
        Voices[0]!.Dispose();
        Voices[0] = null;
      }

      if (Voices[1] != null)
      {
        Voices[1]!.Dispose();
        Voices[1] = null;
      }

      _logger.Debug("LocalTTSEngine disposed");
    }
  }

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool SetDefaultDllDirectories(uint DirectoryFlags);

  [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
  internal static partial int AddDllDirectory(string NewDirectory);

  [LibraryImport("localtts.dll", EntryPoint = "localtts_start")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial IntPtr LocalTTSStart();

  [LibraryImport("localtts.dll", EntryPoint = "localtts_text_2_audio")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial LocalTTSResult LocalTTSText2Audio(IntPtr ctx, IntPtr text, IntPtr voice);

  [LibraryImport("localtts.dll", EntryPoint = "localtts_load_voice")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial IntPtr LocalTTSLoadVoice(IntPtr configBuffer, uint configBufferSize, IntPtr modelBuffer, uint modelBufferSize);

  [LibraryImport("localtts.dll", EntryPoint = "localtts_set_speaker_id")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial void LocalTTSSetSpeakerId(IntPtr voice, long speakerId);

  [LibraryImport("localtts.dll", EntryPoint = "localtts_free_voice")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial void LocalTTSFreeVoice(IntPtr voice);

  [LibraryImport("localtts.dll", EntryPoint = "localtts_free_result")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial void LocalTTSFreeResult(LocalTTSResult result);

  [LibraryImport("localtts.dll", EntryPoint = "localtts_free")]
  [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
  internal static partial void LocalTTSFree(IntPtr ctx);

  public Task<float[]> SpeakTTS(string text, LocalTTSVoice voice)
  {
    return Task.Run(() =>
    {
      _logger.Debug($"TTS for '{text}'");
      SpeechUnit[] units = SSMLPreprocessor.Preprocess(text);
      List<float> samples = [];
      TTSResult result = null!;
      foreach (SpeechUnit unit in units)
      {
        result = SpeakSamples(unit, voice);
        samples.AddRange(result.Samples);
      }

      _logger.Debug($"Done. Returned '{samples.Count}' samples .. result.SampleRate {result.SampleRate}");
      return samples.ToArray();
    });
  }

  private TTSResult SpeakSamples(SpeechUnit unit, LocalTTSVoice voice)
  {
    float[] samples = [];
    using FixedString textPtr = new(unit.Text);
    LocalTTSResult nativeResult = new()
    {
      Channels = 0
    };

    lock (_lock)
    {
      try
      {
        voice.AcquireReaderLock();
        if (_disposed || voice.Disposed)
        {
          samples = [];
          _logger.Error("Couldn't process TTS. TTSEngine or LocalTTSVoice has been disposed.");
          return new TTSResult { Samples = samples, Channels = 0, SampleRate = 0 };
        }

        ValidatePointer(voice.Pointer, "Voice pointer is null.");
        ValidatePointer(voice.ConfigPointer.Address, "Config pointer is null.");
        ValidatePointer(voice.ModelPointer.Address, "Model pointer is null.");
        ValidatePointer(_context, "Context pointer is null.");
        ValidatePointer(textPtr.Address, "Text pointer is null.");

        nativeResult = LocalTTSText2Audio(_context, textPtr.Address, voice.Pointer);
        samples = PtrToSamples(nativeResult.Samples, nativeResult.LengthSamples);
      }
      catch (Exception ex)
      {
        _logger.Error($"Error while processing TTS: {ex}");
      }
      finally
      {
        voice.ReleaseReaderLock();
      }
    }

    TTSResult managedResult = new()
    {
      Channels = nativeResult.Channels,
      SampleRate = nativeResult.SampleRate,
      Samples = samples
    };

    LocalTTSFreeResult(nativeResult);

    return managedResult;
  }

  private void ValidatePointer(IntPtr pointer, string errorMessage)
  {
    if (pointer == IntPtr.Zero)
      throw new InvalidOperationException(errorMessage);
  }

  private float[] PtrToSamples(IntPtr int16Buffer, uint samplesLength)
  {
    float[] floatSamples = new float[samplesLength];
    short[] int16Samples = new short[samplesLength];

    Marshal.Copy(int16Buffer, int16Samples, 0, (int)samplesLength);

    for (int i = 0; i < samplesLength; i++)
    {
      floatSamples[i] = int16Samples[i] / (float)short.MaxValue;
    }

    return floatSamples;
  }
}

public class FixedString(string text) : IDisposable
{
  public IntPtr Address { get; private set; } = Marshal.StringToHGlobalAnsi(text);

  public void Dispose()
  {
    if (Address == IntPtr.Zero) return;
    Marshal.FreeHGlobal(Address);
    Address = IntPtr.Zero;
  }
}

public class TTSResult
{
  public required float[] Samples;
  public required uint Channels;
  public required uint SampleRate;
}

[StructLayout(LayoutKind.Sequential)]
public struct LocalTTSResult
{
  public uint Channels;
  public uint SampleRate;
  public uint LengthSamples;
  public IntPtr Samples;
}
