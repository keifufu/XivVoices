using NumSharp;

namespace XivVoices.Services;

public class LocalTTSVoice
{
  public static LocalTTSVoice FromPath(string filePath)
  {
    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
    return new LocalTTSVoice
    {
      Name = fileNameWithoutExtension.Split("_")[1].FirstCharToUpper(),
      _fileName = fileNameWithoutExtension,
      _features = np.Load<float[,,]>(filePath)
    };
  }

  public string Name { get; init => field = value; } = "";
  public string Gender => _fileName[1] == 'm' ? "Male" : "Female";

  private string _fileName { get; init => field = value; } = "";
  private float[,,] _features { get; init; } = null!;
  public static implicit operator float[,,](LocalTTSVoice voice) => voice._features;
}
