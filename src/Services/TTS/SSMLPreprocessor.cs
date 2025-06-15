namespace XivVoices.Services;

public struct SpeechUnit
{
  public string Text;
}

public static class SSMLPreprocessor
{
  public static SpeechUnit[] Preprocess(string ssml)
  {
    if (ssml.Length == 0) return [];
    List<SpeechUnit> speechUnits = [];
    StringBuilder currentUnit = new();

    using (StringReader reader = new(ssml))
    {
      while (reader.Peek() != -1)
      {
        char nextChar = (char)reader.Read();

        if (nextChar == '<')
        {
          StringBuilder tagBuilder = new();
          while (reader.Peek() != -1 && (nextChar = (char)reader.Read()) != '>')
          {
            tagBuilder.Append(nextChar);
          }

          string tag = tagBuilder.ToString();

          if (tag.StartsWith("break"))
          {
            currentUnit.AppendLine();
            currentUnit.AppendLine();
          }
          else if (tag.StartsWith("prosody"))
          {
            speechUnits.Add(new SpeechUnit { Text = currentUnit.ToString() });
            currentUnit.Clear();
          }
        }
        else
        {
          currentUnit.Append(nextChar);
        }
      }
    }

    if (currentUnit.Length > 0)
    {
      speechUnits.Add(new SpeechUnit { Text = currentUnit.ToString() });
    }

    return [.. speechUnits];
  }
}
