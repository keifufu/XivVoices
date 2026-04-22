namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawOverlaySettingsTab()
  {
    DrawConfigCheckbox("Overlay Open", ref _configuration.OverlayOpen);
    DrawConfigSlider("Overlay Scale", ref _configuration.OverlayScale, 50, 200);
    DrawConfigCheckbox("Overlay Border", ref _configuration.OverlayBorder);
  }
}
