namespace XivVoices.Services;

public class DataStatus
{
  public int VoicelinesDownloaded;

  public bool UpdateInProgress;
  public int UpdateTotalFiles;
  public int UpdateSkippedFiles;
  public int UpdateCompletedFiles;
  public DateTime UpdateStartTime = DateTime.UtcNow;

  public float UpdateProgressPercent => UpdateTotalFiles == 0 ? 0 : (float)UpdateCompletedFiles / (UpdateTotalFiles - UpdateSkippedFiles);

  private DateTime _lastETAUpdateTime = DateTime.MinValue;
  private TimeSpan _lastETA = TimeSpan.MaxValue;
  public TimeSpan UpdateETA
  {
    get
    {
      if (UpdateCompletedFiles == 0) return TimeSpan.MaxValue;

      DateTime currentTime = DateTime.UtcNow;
      if ((currentTime - _lastETAUpdateTime).TotalSeconds >= 0.5)
      {
        _lastETAUpdateTime = currentTime;
        TimeSpan elapsed = DateTime.UtcNow - UpdateStartTime;
        double avgTimePerFile = elapsed.TotalSeconds / UpdateCompletedFiles;
        int remainingFiles = UpdateTotalFiles - UpdateSkippedFiles - UpdateCompletedFiles;
        _lastETA = TimeSpan.FromSeconds(avgTimePerFile * remainingFiles);
      }

      return _lastETA;
    }
  }
}
