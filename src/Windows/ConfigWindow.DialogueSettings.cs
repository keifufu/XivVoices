namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawDialogueSettingsTab()
  {
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Chat Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Queue Chat Messages", ref _configuration.QueueChatMessages);
    DrawConfigCheckbox("Say Enabled", ref _configuration.ChatSayEnabled);
    DrawConfigCheckbox("Tell Enabled", ref _configuration.ChatTellEnabled);
    DrawConfigCheckbox("Shout/Yell Enabled", ref _configuration.ChatShoutYellEnabled);
    DrawConfigCheckbox("Party Enabled", ref _configuration.ChatPartyEnabled);
    DrawConfigCheckbox("Alliance Enabled", ref _configuration.ChatAllianceEnabled);
    DrawConfigCheckbox("Free Company Enabled", ref _configuration.ChatFreeCompanyEnabled);
    DrawConfigCheckbox("Linkshell Enabled", ref _configuration.ChatLinkshellEnabled);
    DrawConfigCheckbox("Emotes Enabled", ref _configuration.ChatEmotesEnabled);

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Dialogue Source Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    using (ImRaii.IEndObject table = ImRaii.Table("AddonSettingsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
    {
      if (!table.Success) return;
      ImGui.TableSetupColumn("Source");
      ImGui.TableSetupColumn("Enabled");
      ImGui.TableSetupColumn("TTS        ");
      ImGui.TableSetupColumn("System Messages");
      ImGui.TableHeadersRow();

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);
      ImGui.TextUnformatted("Dialogue");

      ImGui.TableSetColumnIndex(1);
      DrawConfigCheckbox("AddonTalkEnabled", ref _configuration.AddonTalkEnabled, false);

      ImGui.TableSetColumnIndex(2);
      DrawConfigCheckbox("AddonTalkTTSEnabled", ref _configuration.AddonTalkTTSEnabled, false);

      ImGui.TableSetColumnIndex(3);
      DrawConfigCheckbox("AddonTalkSystemEnabled", ref _configuration.AddonTalkSystemEnabled, false);

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);
      ImGui.TextUnformatted("Battle Dialogue");

      ImGui.TableSetColumnIndex(1);
      DrawConfigCheckbox("AddonBattleTalkEnabled", ref _configuration.AddonBattleTalkEnabled, false);

      ImGui.TableSetColumnIndex(2);
      DrawConfigCheckbox("AddonBattleTalkTTSEnabled", ref _configuration.AddonBattleTalkTTSEnabled, false);

      ImGui.TableSetColumnIndex(3);
      DrawConfigCheckbox("AddonBattleTalkSystemEnabled", ref _configuration.AddonBattleTalkSystemEnabled, false);

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);
      ImGui.TextUnformatted("Bubbles");

      ImGui.TableSetColumnIndex(1);
      DrawConfigCheckbox("AddonMiniTalkEnabled", ref _configuration.AddonMiniTalkEnabled, false);

      ImGui.TableSetColumnIndex(2);
      DrawConfigCheckbox("AddonMiniTalkTTSEnabled", ref _configuration.AddonMiniTalkTTSEnabled, false);
    }

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Other");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Auto-Advance", ref _configuration.AutoAdvanceEnabled);
    string autoAdvanceHelp = """
    Automatically advances to the next dialogue when audio finishes playing.
    Hold ALT on a keyboard, or Y / Triangle on a controller, to temporarily pause it.
    The spinner in the bottom-right of the dialogue box shows the auto-advance status.
    """;
    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.TextUnformatted(autoAdvanceHelp);

    DrawConfigCheckbox("Retainers Enabled", ref _configuration.RetainersEnabled);
    DrawConfigCheckbox("Print System Messages", ref _configuration.PrintSystemMessages);
    DrawConfigCheckbox("Replace Voiced ARR Cutscenes", ref _configuration.ReplaceVoicedARRCutscenes);
  }
}
