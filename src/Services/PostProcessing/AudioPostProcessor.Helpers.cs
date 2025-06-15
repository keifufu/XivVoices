namespace XivVoices.Services;

public partial class AudioPostProcessor
{
  private string GetFFmpegFilterArguments(XivMessage message, bool isLocalTTS)
  {
    int speed = isLocalTTS ? _configuration.LocalTTSSpeed : _configuration.Speed;

    bool changeSpeed = speed != 100;
    string additionalChanges = "";
    if (message.Voice == "Omicron" || message.Voice == "Node" || message.NpcData != null && message.NpcData.Type.Contains("Robot")) additionalChanges = "robot";

    string filterArgs = "";
    bool addEcho = false;

    /* determine a pitch based on string msg.Speaker
    {
      int hash = msg.Speaker == "Bubble" ? msg.Sentence.GetHashCode() : msg.Speaker.GetHashCode();
      float normalized = (hash & 0x7FFFFFFF) / (float)Int32.MaxValue;
      float pitch = (normalized - 0.5f) * 0.5f;
      pitch = (float)Math.Round(pitch * 10) / 50;
      float setRate = 44100 * (1 + pitch);
      float tempo = 1.0f / (1 + pitch);
      Logger.Debug($"Pitch for {msg.Speaker} is {pitch}");
      Logger.Debug($"\"atempo={tempo},asetrate={setRate}\"");
      if (pitch != 0)
      {
        if (filterArgs != "") filterArgs += ",";
        filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
      }
    }
    */

    float setRate = 48000;
    float tempo = 1.0f;

    // Sounds Effects for Age
    if (message.NpcData != null && message.NpcData.Type == "Old")
    {
      setRate *= 1 - 0.1f;
      tempo /= 1 - 0.1f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
    }

    // Sound Effects for Dragons
    if (message.NpcData != null && message.NpcData.Race.StartsWith("Dragon"))
    {
      if (message.NpcData.Type == "Female")
      {
        setRate *= 1 - 0.1f;
        tempo /= 1 + 0.1f;
      }
      else
        switch (message.NpcData.Race)
        {
          case "Dragon_Medium":
            setRate *= 1 - 0.1f;
            tempo /= 1 + 0.1f;
            break;
          case "Dragon_Small":
            setRate *= 1 - 0.03f;
            tempo /= 1 + 0.06f;
            break;
          default:
            setRate *= 1 - 0.05f;
            tempo /= 1 + 0.05f;
            break;
        }

      if (tempo != 1)
      {
        if (filterArgs != "") filterArgs += ",";
        filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
      }

      addEcho = true;
    }

    // Sound Effects for Ea
    if (message.Voice == "Ea")
    {
      filterArgs += "\"[0:a]asplit=2[sc][oc];[sc]rubberband=pitch=0.90[sc];[oc]rubberband=pitch=1.02[oc];[sc][oc]amix=inputs=2:duration=longest,volume=2\"";
      filterArgs += ",\"aecho=0.8:0.88:120:0.4\"";
    }

    // Sound Effects for Golems
    else if (message.NpcData != null && message.NpcData.Race.StartsWith("Golem"))
    {
      setRate *= 1 - 0.15f;
      tempo /= 1 - 0.15f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
    }

    // Sound Effects for Giants
    else if (message.NpcData != null && message.NpcData.Race.StartsWith("Giant"))
    {
      setRate *= 1 - 0.25f;
      tempo /= 1 - 0.15f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
    }

    // Sound Effects for Primals
    if (message.NpcData != null && message.NpcData.Type.StartsWith("Primal"))
      addEcho = true;

    if (message.NpcData != null && message.NpcData.Type == "Primal M1")
    {
      setRate *= 1 - 0.15f;
      tempo /= 1 - 0.1f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
    }

    else if (message.NpcData != null && message.NpcData.Type == "Primal Dual")
    {
      if (message.Speaker == "Thal" || message.Sentence.StartsWith("Nald"))
        filterArgs += "\"rubberband=pitch=0.92\"";
      else if (message.Speaker == "Nald" || message.Sentence.StartsWith("Thal"))
        filterArgs += "\"rubberband=pitch=1.03\"";
      else
        filterArgs += "\"[0:a]asplit=2[sc][oc];[sc]rubberband=pitch=0.93[sc];[oc]rubberband=pitch=1.04[oc];[sc][oc]amix=inputs=2:duration=longest,volume=2\"";
    }

    // Sound Effects for Bosses
    if (message.NpcData != null && message.NpcData.Type.StartsWith("Boss"))
      addEcho = true;

    if (message.NpcData != null && message.NpcData.Type == "Boss F1")
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += "\"[0:a]asplit=2[sc][oc];[sc]rubberband=pitch=0.8[sc];[oc]rubberband=pitch=1.0[oc];[sc][oc]amix=inputs=2:duration=longest,volume=2\"";
    }

    /*
    if (message.NpcData != null && message.NpcData.Race == "Pixie")
    {
        setRate *= (1 + 0.15f);
        tempo /= (1 + 0.1f);
        if (filterArgs != "") filterArgs += ",";
        filterArgs += $"\"atempo={tempo},asetrate={setRate}\"";
    }
    */

    if (addEcho)
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += "\"aecho=0.8:0.9:500:0.1\"";
    }

    if (changeSpeed)
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"[0:a]apad=pad_dur=0.25,atempo={(speed / 100f).ToString(System.Globalization.CultureInfo.InvariantCulture)}\"";
    }

    if (additionalChanges == "robot")
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"flanger=depth=10:delay=15,volume=15dB,aphaser=in_gain=0.4\"";
    }

    return filterArgs;
  }
}
