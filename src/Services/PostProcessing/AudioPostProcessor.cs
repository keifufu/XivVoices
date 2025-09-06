namespace XivVoices.Services;

public interface IAudioPostProcessor : IHostedService
{
  Task<WaveStream?> PostProcessToPCM(string voicelinePath, bool isLocalTTS, XivMessage message);
  Task FFmpegStart();
  Task FFmpegStop();
  Task RefreshFFmpegWineProcessState();
  bool IsMac();
  bool FFmpegWineProcessRunning { get; }
  string FFmpegWineScriptPath { get; }
  int FFmpegWineProcessPort { get; }
  bool FFmpegWineDirty { get; }
}

public partial class AudioPostProcessor(ILogger _logger, Configuration _configuration, IDataService _dataService, IDalamudPluginInterface _pluginInterface) : IAudioPostProcessor
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _ = FFmpegStart();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _ = FFmpegStop();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public async Task<WaveStream?> PostProcessToPCM(string voicelinePath, bool isLocalTTS, XivMessage message)
  {
    if (message.GenerationToken.IsCancellationRequested) return null;

    string filterArguments = GetFFmpegFilterArguments(message, isLocalTTS);
    _logger.Debug($"FFmpeg filter arguments: {filterArguments}");

    if (string.IsNullOrEmpty(filterArguments))
      return voicelinePath.EndsWith(".ogg") ? DecodeOggOpusToPCM(voicelinePath) : DecodeWavIeeeToPCM(voicelinePath);

    string? tempFilePath = _dataService.TempFilePath($"ffmpeg-{Guid.NewGuid()}.ogg");
    if (tempFilePath == null) return null;

    string filterComplexFlag = string.IsNullOrEmpty(filterArguments) ? "" : $"-filter_complex \"{filterArguments}\"";
    string ffmpegArguments = $"-i \"{voicelinePath}\" {filterComplexFlag} -ar 48000 -c:a libopus \"{tempFilePath}\"";

    await ExecuteFFmpegCommand(ffmpegArguments);
    if (!File.Exists(tempFilePath))
    {
      _logger.Debug("FFmpeg did not create a file? retrying once.");
      await ExecuteFFmpegCommand(ffmpegArguments);
      if (!File.Exists(tempFilePath))
      {
        _logger.Error("FFmpeg did not create a file.");
        return null;
      }
    }

    WaveStream waveStream = DecodeOggOpusToPCM(tempFilePath);
    File.Delete(tempFilePath);

    if (message.GenerationToken.IsCancellationRequested) return null;
    return waveStream;
  }

  public static WaveStream DecodeOggOpusToPCM(string filePath)
  {
    using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
    IOpusDecoder decoder = OpusCodecFactory.CreateDecoder(48000, 1);
    OpusOggReadStream oggStream = new OpusOggReadStream(decoder, fileStream);

    List<float> pcmSamples = [];

    while (oggStream.HasNextPacket)
    {
      short[] packet = oggStream.DecodeNextPacket();
      if (packet != null)
      {
        foreach (short sample in packet)
        {
          pcmSamples.Add(sample / 32768f);
        }
      }
    }

    WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
    MemoryStream stream = new();
    using (BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true))
    {
      foreach (float sample in pcmSamples)
      {
        writer.Write(sample);
      }
    }
    stream.Position = 0;
    return new RawSourceWaveStream(stream, waveFormat);
  }

  public static WaveStream DecodeWavIeeeToPCM(string filePath)
  {
    byte[] wavBytes = File.ReadAllBytes(filePath);
    MemoryStream memoryStream = new(wavBytes);
    WaveFileReader reader = new(memoryStream);

    if (reader.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
      throw new InvalidOperationException("Expected IEEE float WAV format.");

    return reader;
  }
}
