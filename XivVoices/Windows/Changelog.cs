namespace XivVoices.Windows;

public static class Changelog
{
  // Newest ones go to the top.
  public readonly static Dictionary<string, string[]> Versions = new() {
    { "1.4.0.0", new[]
    {
      "Refactored configuration window.",
      "Added output driver and device settings."
    }},
    { "1.3.1.0", new[]
    {
      "Updated for 7.5 (API15)",
      "Added a warning if the plugin is outdated.",
    }},
    { "1.3.0.0", new[]
    {
      "Added an overlay window to control playback.",
      "Added '/xivv overlay' and '/xivv overlaycfg'.",
      "Added manual report presets.",
      "Added a voiceline override directory.",
      "Added an option to pause or mute playback while the window is unfocused.",
      "Added an option to fast-forward through dialogue.",
      "Added an option to set the maximum pan for directional audio.",
      "Improved directional audio volume and panning.",
      "Fixed an issue with auto-advance not working when the message contains the player's name.",
      "Fixed LipSync causing NPCs to T-Pose away.",
      "Fixed hourly auto-update potentially causing lag spikes.",
    }},
    { "1.2.0.0", new[]
    {
      "Updated for 7.4/7.4HF1 (API14/NET10)",
      "Added the command '/xivv pause'.",
      "Added option to disable TTS for your own chat messages.",
      "Fixed controller support for temporarily pausing auto-advance.",
      "Fixed an issue where audio would stop playing after a long session.",
      "Fixed certain post-processed lines not being played.",
      "Removed playback history limit.",
      "Queued messages now clear when muting.",
    }},
    { "1.1.0.0", new[] {
      "Added option to prevent accidental dialogue advance.",
      "Added a fallback DirectSound audio device in case WaveOut fails.",
      "Added a warning in chat if the plugin is muted during login.",
      "Added an experimental option to use StreamElements for local TTS.",
      "Added a preliminary authentication flow to support upcoming backend features.",
      "Added a version indicator to the plugin window.",
      "Added the command '/xivv upload-logs'.",
      "Added the command '/xivv version'.",
      "Fixed ffmpeg-wine failing to start on some systems and exiting unexpectedly.",
      "Fixed own chat messages always using the default voice, regardless of gender.",
      "Fixed the updater showing an incorrect number of voicelines downloaded.",
      "Fixed concurrency issues with 'Queue Dialogue' enabled.",
      "Fixed directional chat messages becoming too quiet.",
    }},
    { "1.0.0.0", new[] {
      "Initial rewrite release!",
      "Starting from scratch, this plugin is way more maintainable and extendable than the old one, while maintaining feature parity.",
      "The new data architecture has fixed a lot of issues will make voice generation much simpler in the future.",
      "Improved error handling and logging has been added.",
      "The UI has been mostly revamped! It features a better setup/updater and more configurable options.",
      "Automatic reports now include more information to help us generate the voicelines.",
      "Manual reports are back! You can report any voicelines with a reason in /xivv logs.",
      "Debug options and self-tests have been added, but can you find how to enable them?",
      "And there's more to come!"
    }},
  };
}
