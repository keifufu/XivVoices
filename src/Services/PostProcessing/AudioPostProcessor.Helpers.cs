namespace XivVoices.Services;

public partial class AudioPostProcessor
{
  private string GetFFmpegFilterArguments(XivMessage message, bool isLocalTTS)
  {
    int speed = isLocalTTS ? _configuration.LocalTTSSpeed : _configuration.Speed;
    bool changeSpeed = speed != 100;

    string filterArgs = "";
    bool addEcho = false;
    bool isRobot = false;
    float pitch = 1.0f;
    float tempo = 1.0f;

    if (message.Voice?.Name == "Omicron" || message.Voice?.Name == "Node" || message.Npc?.Name == "Omega")
      isRobot = true;

    // Sounds Effects for Age
    if (message.Npc?.Body == "Elderly")
    {
      pitch -= 0.1f;
      tempo /= 1 - 0.1f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={fts(tempo)},rubberband=pitch={fts(pitch)}\"";
    }

    // Sound Effects for Dragons
    if (message.Npc?.Race.StartsWith("Dragon") ?? false)
    {
      if (message.Npc.Gender == "Female")
      {
        pitch -= 0.1f;
        tempo /= 1 + 0.1f;
      }
      else
        switch (message.Npc.Race)
        {
          case "Dragon_Medium":
            pitch -= 0.1f;
            tempo /= 1 + 0.1f;
            break;
          case "Dragon_Small":
            pitch -= 0.03f;
            tempo /= 1 + 0.06f;
            break;
          default:
            pitch -= 0.05f;
            tempo /= 1 + 0.05f;
            break;
        }

      if (tempo != 1)
      {
        if (filterArgs != "") filterArgs += ",";
        filterArgs += $"\"atempo={fts(tempo)},rubberband=pitch={fts(pitch)}\"";
      }

      addEcho = true;
    }

    // Sound Effects for Ea
    if (message.Voice?.Name == "Ea")
    {
      filterArgs += "\"[0:a]asplit=2[sc][oc];[sc]rubberband=pitch=0.90[sc];[oc]rubberband=pitch=1.02[oc];[sc][oc]amix=inputs=2:duration=longest,volume=2\"";
      filterArgs += ",\"aecho=0.8:0.88:120:0.4\"";
    }

    // Sound Effects for Golems
    else if (message.Npc?.Race.StartsWith("Golem") ?? false)
    {
      pitch -= 0.15f;
      tempo /= 1 - 0.15f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={fts(tempo)},rubberband=pitch={fts(pitch)}\"";
    }

    // Sound Effects for Giants
    else if (message.Npc?.Race.StartsWith("Giant") ?? false)
    {
      pitch -= 0.25f;
      tempo /= 1 - 0.15f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={fts(tempo)},rubberband=pitch={fts(pitch)}\"";
    }

    // Sound Effects for Primals
    if (message.Npc?.Race == "Primal")
      addEcho = true;

    if (message.Npc?.Name == "Ifrit")
    {
      pitch -= 0.15f;
      tempo /= 1 - 0.1f;
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"atempo={fts(tempo)},rubberband=pitch={fts(pitch)}\"";
    }

    else if (message.Npc?.Name == "Nald'thal")
    {
      if (message.Speaker == "Thal" || message.Sentence.StartsWith("Nald"))
        filterArgs += "\"rubberband=pitch=0.92\"";
      else if (message.Speaker == "Nald" || message.Sentence.StartsWith("Thal"))
        filterArgs += "\"rubberband=pitch=1.03\"";
      else
        filterArgs += "\"[0:a]asplit=2[sc][oc];[sc]rubberband=pitch=0.93[sc];[oc]rubberband=pitch=1.04[oc];[sc][oc]amix=inputs=2:duration=longest,volume=2\"";
    }

    if (message.Npc?.Name == "Cloud of Darkness")
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += "\"[0:a]asplit=2[sc][oc];[sc]rubberband=pitch=0.8[sc];[oc]rubberband=pitch=1.0[oc];[sc][oc]amix=inputs=2:duration=longest,volume=2\"";
    }

    if (addEcho)
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += "\"aecho=0.8:0.9:500:0.1\"";
    }

    if (changeSpeed)
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"[0:a]apad=pad_dur=0.25,atempo={fts(speed / 100f)}\"";
    }

    if (isRobot)
    {
      if (filterArgs != "") filterArgs += ",";
      filterArgs += $"\"flanger=depth=10:delay=15,volume=15dB,aphaser=in_gain=0.4\"";
    }

    return filterArgs;
  }

  private string fts(float f) => f.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
