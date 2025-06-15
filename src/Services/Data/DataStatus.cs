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

  public TimeSpan UpdateETA
  {
    get
    {
      if (UpdateCompletedFiles == 0) return TimeSpan.MaxValue;
      TimeSpan elapsed = DateTime.UtcNow - UpdateStartTime;
      double avgTimePerFile = elapsed.TotalSeconds / UpdateCompletedFiles;
      int remainingFiles = UpdateTotalFiles - UpdateSkippedFiles - UpdateCompletedFiles;
      return TimeSpan.FromSeconds(avgTimePerFile * remainingFiles);
    }
  }
}
