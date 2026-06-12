namespace XivVoices.Windows;

public static class Changelog
{
  // Newest ones go to the top.
  public readonly static Dictionary<string, string[]> Versions = new() {
    { "1.5.2.0", new[]
    {
      "Added LocalTTS CPU usage option.",
      "Added import/export for LocalTTS overrides and lexicon.",
      "Added option to have LocalTTS voice your scenario choices.",
      "Added option to respect FFXIV master-volume toggle.",
      "Added chat message filters to LocalTTS lexicon.",
      "Added regex support for LocalTTS lexicon.",
      "Ignored Nicole TTS voice by default.",
      "Made the updater more resilient to failures.",
      "Made voice-randomization persistent when changing allowed voices.",
      "Queued LocalTTS lines now preemptively generate.",
      "Renamed 'audio logs' to 'audio history' (the 'logs' command remains available as an alias for now).",
      "Fixed 'allowed voices' setting having no effect.",
      "Fixed auto-advance failing when the overlay was in the top-left corner (even if disabled).",
      "Fixed pitch randomization being applied to regular voicelines.",
      "Fixed updates failing on slow connections.",
      "Fixed volume issues with directional audio.",
      "Smoothed volume differences between LocalTTS voices.",
      "Use 'yells' and 'shouts' instead of 'says' for those channels."
    }},
    { "1.5.0.0", new[]
    {
      "Added new Local TTS Engine with 27 voices.",
      "Added NPC or Player specific voice and pitch overrides for Local TTS.",
      "Added persistent voice and pitch randomization for Local TTS.",
      "Added a customizable replacement lexicon for Local TTS.",
      "Added an option to ignore chat messages during cutscenes.",
      "Added '/xivv localtts' and '/xivv lexicon'.",
      "Removed FFmpeg dependency and its wine workarounds.",
    }},
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
      "The new data architecture has fixed a lot of issues and will make voice generation much simpler in the future.",
      "Improved error handling and logging has been added.",
      "The UI has been mostly revamped! It features a better setup/updater and more configurable options.",
      "Automatic reports now include more information to help us generate the voicelines.",
      "Manual reports are back! You can report any voicelines with a reason in /xivv logs.",
      "Debug options and self-tests have been added, but can you find how to enable them?",
      "And there's more to come!"
    }},
  };
}
