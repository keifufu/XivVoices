namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawSelfTestTab()
  {
    ImGui.TextUnformatted("WIP");
    // TODO
    // This should help test things we know could break after a patch.
    // At minimum we should test all interop related things. Our own logic
    // is optional at best, that shouldn't randomly break with a game update.
  }
}
