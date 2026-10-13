using NumSharp;

namespace XivVoices.Services;

public class LocalTTSVoice
{
  public static LocalTTSVoice FromPath(string filePath)
  {
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
    return new LocalTTSVoice
    {
      Name = fileNameWithoutExtension.Split("_")[1].FirstCharToUpper(CultureInfo.InvariantCulture),
      FileName = fileNameWithoutExtension,
      Features = np.Load<float[,,]>(filePath)
    };
  }

  public string Name { get; init; } = "";
  public string Gender => FileName[1] == 'm' ? "Male" : "Female";

  private string FileName { get; init; } = "";
  private float[,,] Features { get; init; } = null!;

  public static implicit operator float[,,](LocalTTSVoice voice)
  {
    return voice.Features;
  }
}
