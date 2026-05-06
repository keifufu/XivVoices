namespace XivVoices.Windows;

public partial class ConfigWindow
{
  private void DrawDialogueSettingsTab()
  {
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Chat Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Chat Messages Enabled", ref _configuration.ChatEnabled);
    DrawConfigCheckbox("Say Enabled", ref _configuration.ChatSayEnabled);
    DrawConfigCheckbox("Tell Enabled", ref _configuration.ChatTellEnabled);
    DrawConfigCheckbox("Party Enabled", ref _configuration.ChatPartyEnabled);
    DrawConfigCheckbox("Shout/Yell Enabled", ref _configuration.ChatShoutYellEnabled);
    DrawConfigCheckbox("Alliance Enabled", ref _configuration.ChatAllianceEnabled);
    DrawConfigCheckbox("Free Company Enabled", ref _configuration.ChatFreeCompanyEnabled);
    DrawConfigCheckbox("Linkshell Enabled", ref _configuration.ChatLinkshellEnabled);
    DrawConfigCheckbox("Emotes Enabled", ref _configuration.ChatEmotesEnabled);
    DrawConfigCheckbox("Queue Chat Messages", ref _configuration.QueueChatMessages);
    DrawConfigCheckbox("Add '<Player> says' to chat messages", ref _configuration.LocalTTSPlayerSays);
    DrawConfigCheckbox("Disable voicing your own chat messages", ref _configuration.LocalTTSDisableLocalPlayerChat);

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Dialogue Settings");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("Queue Dialogue", ref _configuration.QueueDialogue);
    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Queues regular dialogue so it won't get skipped when you click away.");

    using (ImRaii.TableDisposable table = ImRaii.Table("AddonSettingsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
    {
      if (!table.Success) return;
      ImGui.TableSetupColumn("Source");
      ImGui.TableSetupColumn("Enabled");
      ImGui.TableSetupColumn("TTS        ");
      ImGui.TableSetupColumn("Narrator Messages");
      ImGui.TableHeadersRow();

      ImGui.TableNextRow();
      ImGui.TableNextColumn();
      ImGui.Text("Dialogue");

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonTalkEnabled", ref _configuration.AddonTalkEnabled, false);

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonTalkTTSEnabled", ref _configuration.AddonTalkTTSEnabled, false);

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonTalkNarratorEnabled", ref _configuration.AddonTalkNarratorEnabled, false);

      ImGui.TableNextRow();
      ImGui.TableNextColumn();
      ImGui.Text("Battle Dialogue");

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonBattleTalkEnabled", ref _configuration.AddonBattleTalkEnabled, false);

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonBattleTalkTTSEnabled", ref _configuration.AddonBattleTalkTTSEnabled, false);

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonBattleTalkNarratorEnabled", ref _configuration.AddonBattleTalkNarratorEnabled, false);

      ImGui.TableNextRow();
      ImGui.TableNextColumn();
      ImGui.Text("Bubbles");

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonMiniTalkEnabled", ref _configuration.AddonMiniTalkEnabled, false);

      ImGui.TableNextColumn();
      DrawConfigCheckbox("AddonMiniTalkTTSEnabled", ref _configuration.AddonMiniTalkTTSEnabled, false);
    }

    ImGui.Unindent(ScaledFloat(8));
    ImGui.Dummy(ScaledVector2(0, 10));
    ImGui.TextWrapped("Other");
    ImGui.Dummy(ScaledVector2(0, 5));
    ImGui.Indent(ScaledFloat(8));

    DrawConfigCheckbox("LipSync Enabled", ref _configuration.LipSyncEnabled);

    DrawConfigCheckbox("Auto-Advance", ref _configuration.AutoAdvanceEnabled);
    string autoAdvanceHelp = """
    Automatically advances to the next dialogue when audio finishes playing.
    Hold ALT on a keyboard, or Y / Triangle on a controller, to temporarily pause it.
    The spinner in the bottom-right of the dialogue box shows the auto-advance status.
    """;
    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text(autoAdvanceHelp);

    DrawConfigCheckbox("Fast Forward", ref _configuration.FastForward);
    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Dialogue boxes will be skipped immediately, useful if used in combination with \"Queue Dialogue\". Will not take effect if muted.");

    DrawConfigCheckbox("Retainers Enabled", ref _configuration.RetainersEnabled);
    DrawConfigCheckbox("Replace Voiced ARR Cutscenes", ref _configuration.ReplaceVoicedARRCutscenes);
    DrawConfigCheckbox("Prevent Accidental Dialogue Advance", ref _configuration.PreventAccidentalDialogueAdvance);
    if (ImGui.IsItemHovered())
      using (ImRaii.Tooltip())
        ImGui.Text("Prevents advancing dialogue when left-clicking unless hovering over the actual dialogue box. (Disabled while plugin is muted)");
  }
}
