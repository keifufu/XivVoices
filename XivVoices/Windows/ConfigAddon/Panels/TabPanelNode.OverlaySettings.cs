using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace XivVoices.Windows;

public class OverlaySettingsTabPanelNode(IServiceProvider _services) : TabPanelNode(container: false)
{
  public override ConfigTab Tab => ConfigTab.OverlaySettings;
  private Configuration _configuration = null!;

  private CheckboxNode _overlayEnabledNode = null!;
  private CheckboxNode _overlayHideInDutyNode = null!;
  private CheckboxNode _overlayHideInCombatNode = null!;
  private CheckboxNode _overlayHideWhenMutedNode = null!;
  private CheckboxNode _overlayShowBorderNode = null!;
  private SliderNode _overlayScaleNode = null!;

  public override void OnSetup()
  {
    _configuration = _services.GetRequiredService<Configuration>();

    ConfigSectionNode overlaySettingsSectionNode = new("Overlay Settings");

    _overlayEnabledNode = new()
    {
      String = "Overlay Enabled",
      Size = new Vector2(150.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.OverlayOpen = value;
        _configuration.Save();
      }
    };
    overlaySettingsSectionNode.AttachNode(_overlayEnabledNode);

    _overlayHideInDutyNode = new()
    {
      String = "Hide while in duty",
      Size = new Vector2(160.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.OverlayHideInDuty = value;
        _configuration.Save();
      }
    };
    overlaySettingsSectionNode.AttachNode(_overlayHideInDutyNode);

    _overlayHideInCombatNode = new()
    {
      String = "Hide while in combat",
      Size = new Vector2(180.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.OverlayHideInCombat = value;
        _configuration.Save();
      }
    };
    overlaySettingsSectionNode.AttachNode(_overlayHideInCombatNode);

    _overlayHideWhenMutedNode = new()
    {
      String = "Hide while muted",
      Size = new Vector2(160.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.OverlayHideWhenMuted = value;
        _configuration.Save();
      }
    };
    overlaySettingsSectionNode.AttachNode(_overlayHideWhenMutedNode);

    AttachNode(overlaySettingsSectionNode);

    ConfigSectionNode overlayAppearanceSectionNode = new("Overlay Appearance", overlaySettingsSectionNode);

    _overlayShowBorderNode = new()
    {
      String = "Show border",
      Size = new Vector2(120.0f, 20.0f),
      OnClick = (value) =>
      {
        _configuration.OverlayBorder = value;
        _configuration.Save();
      }
    };
    overlayAppearanceSectionNode.AttachNode(_overlayShowBorderNode);

    _overlayScaleNode = new()
    {
      Range = 50..200,
      Size = new Vector2(220.0f, 16.0f),
      OnValueChanged = (value) =>
      {
        if (_configuration.OverlayScale != value)
        {
          _configuration.OverlayScale = value;
          _configuration.Save();
        }
      }
    };
    overlayAppearanceSectionNode.AttachNode(_overlayScaleNode, padding: 5.0f);

    overlayAppearanceSectionNode.AttachNode(new LabelTextNode()
    {
      String = "Scale",
      X = 240.0f,
      Height = 16.0f,
      TextColor = ColorHelper.GetColor(2),
    }, inline: true);

    AttachNode(overlayAppearanceSectionNode);
  }

  public override void ConfigurationSaved()
  {
    _overlayEnabledNode.IsChecked = _configuration.OverlayOpen;

    _overlayHideInDutyNode.IsChecked = _configuration.OverlayHideInDuty;
    _overlayHideInDutyNode.IsEnabled = _configuration.OverlayOpen;

    _overlayHideWhenMutedNode.IsChecked = _configuration.OverlayHideWhenMuted;
    _overlayHideWhenMutedNode.IsEnabled = _configuration.OverlayOpen;

    _overlayShowBorderNode.IsChecked = _configuration.OverlayBorder;
    _overlayShowBorderNode.IsEnabled = _configuration.OverlayOpen;

    _overlayScaleNode.Value = _configuration.OverlayScale;
    _overlayScaleNode.IsEnabled = _configuration.OverlayOpen;
    NativeUtils.FixSliderNode(_overlayScaleNode);
  }
}
