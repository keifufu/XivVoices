namespace XivVoices.Windows;

public partial class ConfigWindow
{
  // FFmpeg 'atempo' limitations.
  private readonly int _minSpeed = 50;
  private readonly int _maxSpeed = 200;

  private void DrawAudioSettingsTab()
  {
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Playback Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Mute Enabled", ref _configuration.MuteEnabled);
    DrawConfigCheckbox("LipSync Enabled", ref _configuration.LipSyncEnabled);
    ImGui.Dummy(ScaledVector2(0, 10));
    DrawConfigSlider("Volume", ref _configuration.Volume, 0, 100);
    DrawConfigSlider("Speed", ref _configuration.Speed, _minSpeed, _maxSpeed);

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 25));
    ImGui.TextWrapped("Directional Audio");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Directional Audio (Chat)", ref _configuration.DirectionalAudioForChat);
    DrawConfigCheckbox("Directional Audio (Bubbles)", ref _configuration.DirectionalAudioForAddonMiniTalk);

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 25));
    ImGui.TextWrapped("TTS Playback Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("TTS Enabled", ref _configuration.LocalTTSEnabled);

    ImGui.Dummy(ScaledVector2(0, 10));
    string[] genders = ["Male", "Female"];
    ImGui.Text("TTS Default Voice");
    using (ImRaii.IEndObject combo = ImRaii.Combo("##LocalTTSDefaultVoice", _configuration.LocalTTSDefaultVoice))
    {
      if (combo.Success)
      {
        for (int i = 0; i < genders.Length; i++)
        {
          if (ImGui.Selectable(genders[i]))
          {
            _configuration.LocalTTSDefaultVoice = genders[i];
            _configuration.Save();
          }
        }
      }
    }

    ImGui.Dummy(ScaledVector2(0, 10));

    DrawConfigSlider("TTS Volume", ref _configuration.LocalTTSVolume, 0, 100);
    DrawConfigSlider("TTS Speed", ref _configuration.LocalTTSSpeed, _minSpeed, _maxSpeed);

    ImGui.Dummy(ScaledVector2(0, 10));
    DrawConfigCheckbox("Add '<Player> says' to chat messages", ref _configuration.LocalTTSPlayerSays);
  }
}
