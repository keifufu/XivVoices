namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawOverlaySettingsTab()
  {
    DrawConfigCheckbox("Overlay Enabled", ref _configuration.OverlayOpen);
    DrawConfigSlider("Overlay Scale", ref _configuration.OverlayScale, 50, 200);
    DrawConfigCheckbox("Show Overlay Border", ref _configuration.OverlayBorder);
    DrawConfigCheckbox("Overlay Hidden in Duties", ref _configuration.OverlayHideInDuty);
    DrawConfigCheckbox("Overlay Hidden in Combat", ref _configuration.OverlayHideInCombat);
    DrawConfigCheckbox("Overlay Hidden when Muted", ref _configuration.OverlayHideWhenMuted);
  }
}
