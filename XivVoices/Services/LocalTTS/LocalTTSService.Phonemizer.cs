namespace XivVoices.Services;

public partial class LocalTTSService
{
  private const int espeakINITIALIZE_PHONEME_EVENTS = 0x0001;
  private const int espeakINITIALIZE_PHONEME_IPA = 0x0002;
  private const int AUDIO_OUTPUT_RETRIEVAL = 1;

  private static readonly ConcurrentDictionary<uint, ManualResetEventSlim> _completionEvents = new();
  private static readonly SynthCallbackDelegate _callback = SynthCallback;
  private static readonly List<(int pos, string phoneme)> _phonemes = [];
  private static readonly Lock _phonemesLock = new();

  public void InitializePhonemizer(string toolsDirectory)
  {
    espeak_Initialize(AUDIO_OUTPUT_RETRIEVAL, 0, Path.Join(toolsDirectory, "espeak-ng-data"), espeakINITIALIZE_PHONEME_EVENTS | espeakINITIALIZE_PHONEME_IPA);
    espeak_SetVoiceByName("en-us");
    espeak_SetSynthCallback(_callback);
  }

  public void DisposePhonemizer()
  {
    _ = espeak_Terminate();
    lock (_phonemesLock) { _phonemes.Clear(); }
    _completionEvents.Clear();
  }

  public string Phonemize(string text)
  {
    lock (_phonemesLock) { _phonemes.Clear(); }

    ReadOnlySpan<byte> utf8 = Encoding.UTF8.GetBytes(text + '\0');
    IntPtr mem = Marshal.AllocHGlobal(utf8.Length);
    try
    {
      Marshal.Copy(utf8.ToArray(), 0, mem, utf8.Length);

      int err = espeak_Synth(mem, (UIntPtr)utf8.Length, 0, 1, 0, 1, out uint id, IntPtr.Zero);
      if (err != 0) return "";

      espeak_Synchronize();

      List<(int pos, string phoneme)> snapshot;
      lock (_phonemesLock)
      {
        snapshot = [.. _phonemes];
        _phonemes.Clear();
      }

      SortedDictionary<int, List<string>> map = [];
      foreach ((int pos, string phoneme) in snapshot)
      {
        if (!map.TryGetValue(pos, out List<string>? list)) map[pos] = list = [];
        list.Add(phoneme);
      }

      if (map.Count == 0) return string.Empty;

      List<string> parts = new(map.Count);
      foreach (KeyValuePair<int, List<string>> kv in map)
      {
        parts.Add(string.Concat(kv.Value));
      }

      return string.Join(" ", parts).Replace("  ", "\n").Trim();
    }
    finally
    {
      Marshal.FreeHGlobal(mem);
    }
  }

  private static int SynthCallback(IntPtr wav, int numsamples, IntPtr eventsPtr)
  {
    if (eventsPtr == IntPtr.Zero) return 0;

    int eventSize = Marshal.SizeOf<espeak_EVENT>();
    int offset = 0;

    while (true)
    {
      try
      {
        IntPtr evtPtr = IntPtr.Add(eventsPtr, offset);
        espeak_EVENT raw = Marshal.PtrToStructure<espeak_EVENT>(evtPtr);

        if (raw.type == espeak_EVENT_TYPE.espeakEVENT_LIST_TERMINATED || raw.type == espeak_EVENT_TYPE.espeakEVENT_MSG_TERMINATED)
        {
          if (_completionEvents.TryGetValue(raw.unique_identifier, out ManualResetEventSlim? mre)) mre.Set();
          if (raw.type == espeak_EVENT_TYPE.espeakEVENT_LIST_TERMINATED) break;
        }

        if (raw.type == espeak_EVENT_TYPE.espeakEVENT_PHONEME)
        {
          IntPtr inlineBytesAddr = IntPtr.Add(evtPtr, Marshal.OffsetOf<espeak_EVENT>("id_ptr").ToInt32());
          byte[] buf = new byte[8];
          Marshal.Copy(inlineBytesAddr, buf, 0, buf.Length);
          int len = Array.IndexOf(buf, (byte)0);
          if (len < 0) len = buf.Length;
          string name = Encoding.UTF8.GetString(buf, 0, len).Trim('\0');

          lock (_phonemesLock)
          {
            _phonemes.Add((raw.text_position, name));
          }
        }
      }
      catch
      {
        break;
      }

      offset += eventSize;
    }

    return 0;
  }

  private enum espeak_EVENT_TYPE : int
  {
    espeakEVENT_LIST_TERMINATED = 0,
    espeakEVENT_MSG_TERMINATED = 6,
    espeakEVENT_PHONEME = 7,
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct espeak_EVENT
  {
    public espeak_EVENT_TYPE type;
    public uint unique_identifier;
    public int text_position;
    public int length;
    public int audio_position;
    public int sample;
    public IntPtr user_data;
    public IntPtr id_ptr;
  }

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SynthCallbackDelegate(IntPtr wav, int numsamples, IntPtr events);

  [LibraryImport("espeak-ng", StringMarshalling = StringMarshalling.Utf8)]
  private static partial int espeak_Initialize(int output, int buflength, string path, int options);

  [LibraryImport("espeak-ng")]
  private static partial void espeak_SetSynthCallback(SynthCallbackDelegate callback);

  [LibraryImport("espeak-ng")]
  private static partial int espeak_Synth(IntPtr text, UIntPtr size, uint position, int position_type, uint end_position, uint flags, out uint unique_identifier, IntPtr user_data);

  [LibraryImport("espeak-ng", StringMarshalling = StringMarshalling.Utf8)]
  private static partial int espeak_SetVoiceByName(string name);

  [LibraryImport("espeak-ng")]
  private static partial int espeak_Terminate();

  [LibraryImport("espeak-ng")]
  private static partial void espeak_Synchronize();
}
