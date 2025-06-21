using System.Security.Cryptography;

namespace XivVoices.Services;

public partial class MessageDispatcher
{
  // We used to sanitize this in this way:
  // string sanitizedSentence = Regex.Replace(sentence, @"[^a-zA-Z]", "");
  // and compare that, but that can lead to some non-retainer lines being
  // matches as a retainer line. Perhaps Manifest.Retainers will need
  // updating now.
  private string GetRetainerSpeaker(string speaker, string sentence)
  {
    if (_dataService.Manifest == null) return speaker;

    foreach (string? key in _dataService.Manifest.Retainers.Keys)
    {
      if (key.Equals(sentence))
      {
        return _dataService.Manifest.Retainers[key];
      }
    }

    return speaker;
  }

  // Try to get a voiceline filepath given a cleaned speaker and sentence and optionally NpcData.
  private Task<(string? voicelinePath, string voice)> TryGetVoicelinePath(string speaker, string sentence, NpcData? npcData)
  {
    return Task.Run(() =>
    {
      string voice = "Unknown";
      if (_dataService.Manifest == null) return (null, voice);

      if (speaker == "???" && _dataService.Manifest.Nameless.TryGetValue(sentence, out string? v1))
      {
        // If the speaker is "???", try getting it from Manifest.Nameless
        speaker = v1;
        voice = v1;
        if (_dataService.Manifest.Voices.TryGetValue(speaker, out string? v2))
          voice = v2;
      }
      else if (_dataService.Manifest.Voices.TryGetValue(speaker, out string? v2))
      {
        // Else try to get the voice from Manifest.Voices based on the speaker
        // This is used for non-generic voies
        voice = v2;
      }
      else
      {
        // If no voice was found, get the generic voice from npcData, e.g. "Au_Ra_Raen_Female_05"
        if (npcData == null) return (null, voice); // If we have no NpcData, ggwp. We can't get a generic voice without npcData.
        voice = GetGenericVoice(npcData, speaker);
      }

      _logger.Debug($"voice::{voice} speaker::{speaker} sentence::{sentence}");
      string voicelinePath = Path.Join(_dataService.VoicelinesDirectory, Md5(voice, speaker, sentence) + ".ogg");
      _logger.Debug($"voicelinePath::{voicelinePath}");

      if (!File.Exists(voicelinePath)) return (null, voice);
      return ((string?)voicelinePath, voice);
    });
  }

  private string Md5(params string[] inputs)
  {
    string combinedInput = string.Join(":", inputs);
    byte[] inputBytes = Encoding.UTF8.GetBytes(combinedInput);
    byte[] hashBytes = MD5.HashData(inputBytes);
    StringBuilder sb = new();
    foreach (byte b in hashBytes)
      sb.Append(b.ToString("x2"));
    return sb.ToString();
  }

  // Sanitizes the speaker and sentence. This should preferably NEVER be changed,
  // as that would break a lot of voicelines generated before then.
  // If we do want to add something here, make SURE it will NOT affect existing lines.
  public async Task<(string speaker, string sentence)> CleanMessage(string _speaker, string _sentence, bool keepName = false)
  {
    string speaker = _speaker;
    string sentence = _sentence;
    string pattern;

    // Speaker Sanitization
    {
      // Remove '!' and '?' from speaker
      if (speaker != "???")
        speaker = speaker.Replace("!", "").Replace("?", "");

      // Remove suffixes from speaker
      string[] suffixes = ["'s Voice", "'s Avatar"];
      foreach (string suffix in suffixes)
      {
        if (speaker.EndsWith(suffix))
        {
          speaker = speaker[..^suffix.Length];
          break;
        }
      }
    }

    // Generic Sentence Sanitization
    {
      // Remove text in and including angled brackets, e.g. <sigh> <sniffle>
      sentence = Regex.Replace(sentence, "<[^<]*>", "");

      // Convert Roman Numerals
      sentence = ConvertRomanNumerals(sentence);

      // Replace special characters with ASCII equivalents
      sentence = Regex.Replace(sentence, @"\s+", " ");
      sentence = sentence
        .Replace("─", " - ")
        .Replace("—", " - ")
        .Replace("–", "-")
        .Replace("\n", " ");

      if (!keepName)
      {
        string? fullLocalName = await _framework.RunOnFrameworkThread(() => _clientState.LocalPlayer?.Name.TextValue ?? null);
        if (fullLocalName != null)
        {
          string[] fullname = fullLocalName.Split(" ");

          // Replace 'full name' with 'firstName'
          pattern = "\\b" + fullname[0] + " " + fullname[1] + "\\b";
          sentence = Regex.Replace(sentence, pattern, fullname[0]);

          // Replace 'lastName' with 'firstName'
          pattern = "\\b" + fullname[1] + "\\b";
          sentence = Regex.Replace(sentence, pattern, fullname[0]);

          // Replace 'firstName' with '_NAME_'
          // Note: We used to prevent replacing here if the name was followed by "of the".
          // I can only assume this was because of a few lines saying "Arc of the Worthy",
          // but a good chunk of WHM and BLM quests call you <name> of the <white/black>.
          // Old pattern: "(?<!the )\\b" + fullname[0] + "\\b(?! of the)"
          pattern = "(?<!the )\\b" + fullname[0];
          sentence = Regex.Replace(sentence, pattern, "_NAME_");
        }
      }

      // "Send help" was the original comment for this, idk.
      // Seems to un-escaping unicode. I don't think any of the
      // game npc dialogue have these, so it might be for Chat/LocalTTS?
      sentence = sentence
        .Replace("\\u00e1", "á")
        .Replace("\\u00e9", "é")
        .Replace("\\u00ed", "í")
        .Replace("\\u00f3", "ó")
        .Replace("\\u00fa", "ú")
        .Replace("\\u00f1", "ñ")
        .Replace("\\u00e0", "à")
        .Replace("\\u00e8", "è")
        .Replace("\\u00ec", "ì")
        .Replace("\\u00f2", "ò")
        .Replace("\\u00f9", "ù");

      // Fix Three Dots between letters that have no spaces like "hi...there" to "hi... there"
      sentence = Regex.Replace(sentence, @"(\.{3})(\w)", "$1 $2");

      // Replace normal quotes with quote symbols "" -> “”
      sentence = Regex.Replace(sentence, "[“”]", "\"");
      bool isOpeningQuote = true;
      sentence = Regex.Replace(sentence, "\"", match =>
      {
        if (isOpeningQuote)
        {
          isOpeningQuote = false;
          return "“";
        }
        else
        {
          isOpeningQuote = true;
          return "”";
        }
      });

      // Specific Sentence Sanitization
      {
        // 1-  Cactpot Broker Drawing numbers removal
        pattern = @"Come one, come all - drawing number \d{4}";
        sentence = Regex.Replace(sentence, pattern, "Come one, come all - drawing number");

        // 2-  Cactpot Winning Prize
        if (speaker == "Cactpot Cashier" && sentence.StartsWith("Congratulations! You have won"))
          sentence = "Congratulations! You have won!";

        // 3- Delivery Moogle carrier level removal
        pattern = @"Your postal prowess has earned you carrier level \d{2}";
        sentence = Regex.Replace(sentence, pattern, "Your postal prowess has earned you this carrier level");

        // 4- Chocobo Eligible to Participate In Races
        pattern = @"^Congratulations.*eligible to participate in sanctioned chocobo races\.*";
        sentence = Regex.Replace(sentence, pattern, "Congratulations! Your chocobo is now eligible to participate in sanctioned chocobo races.");

        // 5- Chocobo Training
        pattern = @"^What sort of training did you have in mind for .*, (madam|sir)\?$";
        sentence = Regex.Replace(sentence, pattern, "What sort of training did you have in mind for your chocobo?");

        // 6- Teaching Chocobo an Ability
        pattern = @"^You wish to teach .*, (madam|sir)\? Then, if you would be so kind as to provide the requisite manual\.$";
        sentence = Regex.Replace(sentence, pattern, "You wish to teach your chocobo an ability? Then, if you would be so kind as to provide the requisite manual.");

        // 7- Removing Chocobo Ability
        pattern = @"^You wish for .+ to unlearn an ability\? Very well, if you would be so kind as to specify the undesired ability\.\.\.$";
        sentence = Regex.Replace(sentence, pattern, "You wish for your chocobo to unlearn an ability? Very well, if you would be so kind as to specify the undesired ability...");

        // 8- Feo Ul Lines
        if (speaker == "Feo Ul")
        {
          if (sentence.StartsWith("A whispered word, and off"))
            sentence = "A whispered word, and off goes yours on a grand adventure! What wonders await at journey's end?";
          else if (sentence.StartsWith("Carried by the wind, the leaf flutters to the ground"))
            sentence = "Carried by the wind, the leaf flutters to the ground - and so does yours return to your side. Was the journey a fruitful one?";
          else if (sentence.StartsWith("From verdant green to glittering gold"))
            sentence = "From verdant green to glittering gold, so does the leaf take on delightful hues with each new season. If you would see yours dressed in new colors, your beautiful branch will attend to the task.";
          else if (sentence.StartsWith("Oh, my adorable sapling! You have need"))
            sentence = "Oh, my adorable sapling! You have need of yours, yes? But sing the word, and let your beautiful branch do what only they can.";
          else if (sentence.StartsWith("Very well. I shall slip quietly from"))
            sentence = "Very well. I shall slip quietly from your servant's dreams. May your leaf flutter, float, and find a way back to you.";
          else if (sentence.StartsWith("You have no more need of"))
            sentence = "You have no more need of yours? So be it! I shall steal quietly from your loyal servant's dreams.";
        }

        // 9 - Lady Luck
        pattern = @"And the winning number for draw \d+ is... \d+!";
        sentence = Regex.Replace(sentence, pattern, "And here is the winning number!");
        pattern = @"And the Early Bird Bonus grants everyone an extra \d+%! Make sure you lucky folk claim your winnings promptly!";
        sentence = Regex.Replace(sentence, pattern, "And the Early Bird Bonus grants everyone an extra! Make sure you lucky folk claim your winnings promptly!");

        // 10- Jumbo Cactpot Broker
        pattern = @"Welcome to drawing number \d+ of the Jumbo Cactpot! Can I interest you in a ticket to fame and fortune?";
        sentence = Regex.Replace(sentence, pattern, "Welcome to drawing number of the Jumbo Cactpot! Can I interest you in a ticket to fame and fortune?");

        // 11- Gold Saucer Attendant
        pattern = @"Tickets for drawing number \d+ of the Mini Cactpot are now on sale. To test your fortunes, make your way to Entrance Square!";
        sentence = Regex.Replace(sentence, pattern, "Tickets for this drawing number of the Mini Cactpot are now on sale. To test your fortunes, make your way to Entrance Square!");
        pattern = @"Entries are now being accepted for drawing number \d+ of the Mini Cactpot! Venture to Entrance Square to test your luck!";
        sentence = Regex.Replace(sentence, pattern, "Entries are now being accepted for this drawing number of the Mini Cactpot! Venture to Entrance Square to test your luck!");
        pattern = @"Entries for drawing number \d+ of the Mini Cactpot will close momentarily. Those still wishing to purchase a ticket are encouraged to act quickly!";
        sentence = Regex.Replace(sentence, pattern, "Entries for the drawing number of the Mini Cactpot will close momentarily. Those still wishing to purchase a ticket are encouraged to act quickly!");

        // 12- Delivery Moogle
        pattern = @"Your mailbox is a complete and utter mess! There wasn't any room left, so I had to send back \d+ letters, kupo!";
        sentence = Regex.Replace(sentence, pattern, "Your mailbox is a complete and utter mess! There wasn't any room left, so I had to send back some letters, kupo!");

        // 13- Mini Cactpot Broker
        if (speaker == "Mini Cactpot Broker")
        {
          if (sentence.StartsWith("We have a winner! Please accept my congratulations"))
            sentence = "We have a winner! Please accept my congratulations!";
          if (sentence.StartsWith("Congratulations! Here is your prize"))
            sentence = "Congratulations! Here is your prize. Would you like to purchase another ticket?";
        }
      }

      // Reduce multiple whitespaces to one and Trim().
      sentence = Regex.Replace(sentence, @"\s+", " ").Trim();

      // This is at the end because of lines like "<gulp> <gulp> <gulp> ...See, empty already."
      // Remove leading "..." if present
      if (sentence.Trim().StartsWith("..."))
        sentence = sentence[3..];

      // Yes this second trim is intended.
      // Reduce multiple whitespaces to one and Trim().
      sentence = Regex.Replace(sentence, @"\s+", " ").Trim();
    }

    return (speaker, sentence);
  }

  /*
  This is 'GetOtherVoiceNames' from old xivv. Can't quite clean this up because voices are
  sometimes "_05_06" where two eyeshapes share a voice, or other such cases.
  This is what I would do otherwise:

  var validRaces = new string[] { "Au Ra", "Elezen", "Hrothgar", "Hyur", "Lalafell", "Miqo'te", "Roegadyn", "Viera" };
  var validTribes = new string[] { "Raen", "Xaela", "Duskwight", "Wildwood", "Helions", "The Lost", "Highlander", "Midlander", "Dunesfolk", "Plainsfolk", "Keeper of the Moon", "Seeker of the Sun", "Hellsguard", "Sea Wolf", "Rava", "Veena" };

  if (npcData.Body == "Adult")
  {
    if (validRaces.Contains(npcData.Race) && validTribes.Contains(npcData.Tribe))
      return $"{npcData.Race.Replace(" ", "_").Replace("'", "")}_{npcData.Tribe.Replace(" ", "_")_{npcData.Gender}_{npcData.Eyes.Replace("Option ", "0")}}"
  }

  ...
  */
  public string GetGenericVoice(NpcData npcData, string speaker)
  {
    if (npcData.Body == "Adult")
    {
      if (npcData.Race == "Au Ra")
      {
        if (npcData.Tribe == "Raen" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Au_Ra_Raen_Female_01";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Au_Ra_Raen_Female_02";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Au_Ra_Raen_Female_03";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Au_Ra_Raen_Female_04";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Au_Ra_Raen_Female_05";

        if (npcData.Tribe == "Raen" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Au_Ra_Raen_Male_01";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Au_Ra_Raen_Male_02";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Au_Ra_Raen_Male_03";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Au_Ra_Raen_Male_04";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Au_Ra_Raen_Male_05";
        if (npcData.Tribe == "Raen" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Au_Ra_Raen_Male_06";

        if (npcData.Tribe == "Xaela" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Au_Ra_Xaela_Female_01";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Au_Ra_Xaela_Female_02";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Au_Ra_Xaela_Female_03";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Au_Ra_Xaela_Female_04";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Au_Ra_Xaela_Female_05";

        if (npcData.Tribe == "Xaela" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Au_Ra_Xaela_Male_01";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Au_Ra_Xaela_Male_02";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Au_Ra_Xaela_Male_03";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Au_Ra_Xaela_Male_04";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Au_Ra_Xaela_Male_05";
        if (npcData.Tribe == "Xaela" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Au_Ra_Xaela_Male_06";
      }

      if (npcData.Race == "Elezen")
      {
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Elezen_Duskwight_Female_01";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Elezen_Duskwight_Female_02";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Elezen_Duskwight_Female_03";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Elezen_Duskwight_Female_04";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Elezen_Duskwight_Female_05_06";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Elezen_Duskwight_Female_05_06";

        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Elezen_Duskwight_Male_01";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Elezen_Duskwight_Male_02";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Elezen_Duskwight_Male_03";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Elezen_Duskwight_Male_04";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Elezen_Duskwight_Male_05";
        if (npcData.Tribe == "Duskwight" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Elezen_Duskwight_Male_06";

        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Elezen_Wildwood_Female_01";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Elezen_Wildwood_Female_02";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Elezen_Wildwood_Female_03";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Elezen_Wildwood_Female_04";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Elezen_Wildwood_Female_05";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Elezen_Wildwood_Female_06";

        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Elezen_Wildwood_Male_01";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Elezen_Wildwood_Male_02";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Elezen_Wildwood_Male_03";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Elezen_Wildwood_Male_04";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Elezen_Wildwood_Male_05";
        if (npcData.Tribe == "Wildwood" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Elezen_Wildwood_Male_06";
      }

      if (npcData.Race == "Hrothgar")
      {
        if (npcData.Tribe == "Helions" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Hrothgar_Helion_01_05";
        if (npcData.Tribe == "Helions" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Hrothgar_Helion_02";
        if (npcData.Tribe == "Helions" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Hrothgar_Helion_03";
        if (npcData.Tribe == "Helions" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Hrothgar_Helion_04";
        if (npcData.Tribe == "Helions" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Hrothgar_Helion_01_05";

        if (npcData.Tribe == "The Lost" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Hrothgar_The_Lost_01";
        if (npcData.Tribe == "The Lost" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Hrothgar_The_Lost_02";
        if (npcData.Tribe == "The Lost" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Hrothgar_The_Lost_03";
        if (npcData.Tribe == "The Lost" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Hrothgar_The_Lost_04_05";
        if (npcData.Tribe == "The Lost" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Hrothgar_The_Lost_04_05";
      }

      if (npcData.Race == "Hyur")
      {
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Hyur_Highlander_Female_01";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Hyur_Highlander_Female_02";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Hyur_Highlander_Female_03";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Hyur_Highlander_Female_04";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Hyur_Highlander_Female_05";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Hyur_Highlander_Female_06";

        if (npcData.Tribe == "Highlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Hyur_Highlander_Male_01";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Hyur_Highlander_Male_02";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Hyur_Highlander_Male_03";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Hyur_Highlander_Male_04";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Hyur_Highlander_Male_05";
        if (npcData.Tribe == "Highlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Hyur_Highlander_Male_06";

        if (npcData.Tribe == "Midlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Hyur_Midlander_Female_01";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Hyur_Midlander_Female_02";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Hyur_Midlander_Female_03";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Hyur_Midlander_Female_04";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Hyur_Midlander_Female_05";

        if (npcData.Tribe == "Midlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Hyur_Midlander_Male_01";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Hyur_Midlander_Male_02";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Hyur_Midlander_Male_03";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Hyur_Midlander_Male_04";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Hyur_Midlander_Male_05";
        if (npcData.Tribe == "Midlander" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Hyur_Midlander_Male_06";
      }

      if (npcData.Race == "Lalafell")
      {
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Lalafell_Dunesfolk_Female_01";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Lalafell_Dunesfolk_Female_02";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Lalafell_Dunesfolk_Female_03";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Lalafell_Dunesfolk_Female_04";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Lalafell_Dunesfolk_Female_05";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Lalafell_Dunesfolk_Female_06";

        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Lalafell_Dunesfolk_Male_01";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Lalafell_Dunesfolk_Male_02";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Lalafell_Dunesfolk_Male_03";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Lalafell_Dunesfolk_Male_04";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Lalafell_Dunesfolk_Male_05";
        if (npcData.Tribe == "Dunesfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Lalafell_Dunesfolk_Male_06";

        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Lalafell_Plainsfolk_Female_01";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Lalafell_Plainsfolk_Female_02";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Lalafell_Plainsfolk_Female_03";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Lalafell_Plainsfolk_Female_04";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Lalafell_Plainsfolk_Female_05";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Lalafell_Plainsfolk_Female_06";

        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Lalafell_Plainsfolk_Male_01";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Lalafell_Plainsfolk_Male_02";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Lalafell_Plainsfolk_Male_03";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Lalafell_Plainsfolk_Male_04";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Lalafell_Plainsfolk_Male_05";
        if (npcData.Tribe == "Plainsfolk" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Lalafell_Plainsfolk_Male_06";
      }

      if (npcData.Race == "Miqo'te")
      {
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Miqote_Keeper_of_the_Moon_Female_01";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Miqote_Keeper_of_the_Moon_Female_02";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Miqote_Keeper_of_the_Moon_Female_03";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Miqote_Keeper_of_the_Moon_Female_04";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Miqote_Keeper_of_the_Moon_Female_05";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Miqote_Keeper_of_the_Moon_Female_06";

        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Miqote_Keeper_of_the_Moon_Male_01";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Miqote_Keeper_of_the_Moon_Male_02_06";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Miqote_Keeper_of_the_Moon_Male_03";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Miqote_Keeper_of_the_Moon_Male_04";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Miqote_Keeper_of_the_Moon_Male_05";
        if (npcData.Tribe == "Keeper of the Moon" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Miqote_Keeper_of_the_Moon_Male_02_06";

        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Miqote_Seeker_of_the_Sun_Female_01";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Miqote_Seeker_of_the_Sun_Female_02";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Miqote_Seeker_of_the_Sun_Female_03";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Miqote_Seeker_of_the_Sun_Female_04";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Miqote_Seeker_of_the_Sun_Female_05";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
          return "Miqote_Seeker_of_the_Sun_Female_06";

        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Miqote_Seeker_of_the_Sun_Male_01";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Miqote_Seeker_of_the_Sun_Male_02";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Miqote_Seeker_of_the_Sun_Male_03";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Miqote_Seeker_of_the_Sun_Male_04";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Miqote_Seeker_of_the_Sun_Male_05";
        if (npcData.Tribe == "Seeker of the Sun" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
          return "Miqote_Seeker_of_the_Sun_Male_06";

        //if (npcData.Tribe == "Fat Cat")
        //    return "Miqote_Fat";
      }

      if (npcData.Race == "Roegadyn")
      {
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Roegadyn_Hellsguard_Female_01";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Roegadyn_Hellsguard_Female_02";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Roegadyn_Hellsguard_Female_03";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Roegadyn_Hellsguard_Female_04";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Roegadyn_Hellsguard_Female_05";

        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Roegadyn_Hellsguard_Male_01";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Roegadyn_Hellsguard_Male_02";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Roegadyn_Hellsguard_Male_03";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Roegadyn_Hellsguard_Male_04";
        if (npcData.Tribe == "Hellsguard" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Roegadyn_Hellsguard_Male_05";

        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Roegadyn_Sea_Wolves_Female_01";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Roegadyn_Sea_Wolves_Female_02";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Roegadyn_Sea_Wolves_Female_03";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Roegadyn_Sea_Wolves_Female_04";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Roegadyn_Sea_Wolves_Female_05";

        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Roegadyn_Sea_Wolves_Male_01";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Roegadyn_Sea_Wolves_Male_02";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Roegadyn_Sea_Wolves_Male_03";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Roegadyn_Sea_Wolves_Male_04";
        if (npcData.Tribe == "Sea Wolf" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
          return "Roegadyn_Sea_Wolves_Male_05";
      }

      if (npcData.Race == "Viera")
      {
        if (npcData.Tribe == "Rava" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Viera_Rava_Female_01_05";
        if (npcData.Tribe == "Rava" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Viera_Rava_Female_02";
        if (npcData.Tribe == "Rava" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Viera_Rava_Female_03";
        if (npcData.Tribe == "Rava" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Viera_Rava_Female_04";
        if (npcData.Tribe == "Rava" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Viera_Rava_Female_01_05";

        if (npcData.Tribe == "Rava" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
          return "Viera_Rava_Male_01";
        if (npcData.Tribe == "Rava" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Viera_Rava_Male_03";
        if (npcData.Tribe == "Rava" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
          return "Viera_Rava_Male_04";

        if (npcData.Tribe == "Veena" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
          return "Viera_Veena_Female_01_05";
        if (npcData.Tribe == "Veena" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
          return "Viera_Veena_Female_02";
        if (npcData.Tribe == "Veena" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
          return "Viera_Veena_Female_03";
        if (npcData.Tribe == "Veena" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
          return "Viera_Veena_Female_04";
        if (npcData.Tribe == "Veena" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
          return "Viera_Veena_Female_01_05";

        if (npcData.Tribe == "Veena" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
          return "Viera_Veena_Male_02";
        if (npcData.Tribe == "Veena" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
          return "Viera_Veena_Male_03";
      }
    }

    if (npcData.Body == "Elderly")
    {
      if (npcData.Race == "Hyur" && npcData.Gender == "Male")
        return "Elderly_Male_Hyur";

      if (npcData.Gender == "Male")
        return "Elderly_Male";

      if (npcData.Gender == "Female")
        return "Elderly_Female";
    }

    if (npcData.Body == "Child")
    {
      if (npcData.Race == "Hyur" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
        return "Child_Hyur_Female_1";
      if (npcData.Race == "Hyur" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
        return "Child_Hyur_Female_2";
      if (npcData.Race == "Hyur" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
        return "Child_Hyur_Female_3_5";
      if (npcData.Race == "Hyur" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
        return "Child_Hyur_Female_4";
      if (npcData.Race == "Hyur" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
        return "Child_Hyur_Female_3_5";

      if (npcData.Race == "Hyur" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
        return "Child_Hyur_Male_1";
      if (npcData.Race == "Hyur" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
        return "Child_Hyur_Male_2";
      if (npcData.Race == "Hyur" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
        return "Child_Hyur_Male_3_6";
      if (npcData.Race == "Hyur" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
        return "Child_Hyur_Male_4";
      if (npcData.Race == "Hyur" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
        return "Child_Hyur_Male_5";
      if (npcData.Race == "Hyur" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
        return "Child_Hyur_Male_3_6";

      if (npcData.Race == "Elezen" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
        return "Child_Elezen_Female_1_3";
      if (npcData.Race == "Elezen" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
        return "Child_Elezen_Female_2";
      if (npcData.Race == "Elezen" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
        return "Child_Elezen_Female_1_3";
      if (npcData.Race == "Elezen" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
        return "Child_Elezen_Female_4";
      if (npcData.Race == "Elezen" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
        return "Child_Elezen_Female_5_6";
      if (npcData.Race == "Elezen" && npcData.Gender == "Female" && npcData.Eyes == "Option 6")
        return "Child_Elezen_Female_5_6";

      if (npcData.Race == "Elezen" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
        return "Child_Elezen_Male_1";
      if (npcData.Race == "Elezen" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
        return "Child_Elezen_Male_2";
      if (npcData.Race == "Elezen" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
        return "Child_Elezen_Male_3";
      if (npcData.Race == "Elezen" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
        return "Child_Elezen_Male_4";
      if (npcData.Race == "Elezen" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
        return "Child_Elezen_Male_5_6";
      if (npcData.Race == "Elezen" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
        return "Child_Elezen_Male_5_6";

      if (npcData.Race == "Au Ra" && npcData.Gender == "Female" && npcData.Eyes == "Option 1")
        return "Child_Aura_Female_1_5";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
        return "Child_Aura_Female_2";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
        return "Child_Aura_Female_4";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Female" && npcData.Eyes == "Option 5")
        return "Child_Aura_Female_1_5";

      if (npcData.Race == "Au Ra" && npcData.Gender == "Male" && npcData.Eyes == "Option 1")
        return "Child_Aura_Male_1";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Male" && npcData.Eyes == "Option 2")
        return "Child_Aura_Male_2";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Male" && npcData.Eyes == "Option 3")
        return "Child_Aura_Male_3";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Male" && npcData.Eyes == "Option 4")
        return "Child_Aura_Male_4";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Male" && npcData.Eyes == "Option 5")
        return "Child_Aura_Male_5_6";
      if (npcData.Race == "Au Ra" && npcData.Gender == "Male" && npcData.Eyes == "Option 6")
        return "Child_Aura_Male_5_6";

      if (npcData.Race == "Miqo'te" && npcData.Gender == "Female" && npcData.Eyes == "Option 2")
        return "Child_Miqote_Female_2";
      if (npcData.Race == "Miqo'te" && npcData.Gender == "Female" && npcData.Eyes == "Option 3")
        return "Child_Miqote_Female_3_4";
      if (npcData.Race == "Miqo'te" && npcData.Gender == "Female" && npcData.Eyes == "Option 4")
        return "Child_Miqote_Female_3_4";
    }

    // ARR Beast Tribes
    if (npcData.Race == "Amalj'aa")
      return "Amaljaa";

    if (npcData.Race == "Sylph")
      return "Sylph";

    if (npcData.Race == "Kobold")
      return "Kobold";

    if (npcData.Race == "Sahagin")
      return "Sahagin";

    if (npcData.Race == "Ixal")
      return "Ixal";

    if (npcData.Race == "Qiqirn")
      return "Qiqirn";

    // HW Beast Tribes
    if (npcData.Race.StartsWith("Dragon"))
      return npcData.Race;

    if (npcData.Race == "Goblin")
    {
      if (npcData.Gender == "Female")
        return "Goblin_Female";
      else
        return "Goblin_Male";
    }

    if (npcData.Race == "Vanu Vanu")
    {
      if (npcData.Gender == "Female")
        return "Vanu_Female";
      else
        return "Vanu_Male";
    }

    if (npcData.Race == "Vath")
      return "Vath";

    if (npcData.Race == "Moogle")
      return "Moogle";

    if (npcData.Race == "Node")
      return "Node";

    // SB Beast Tribes
    if (npcData.Race == "Kojin")
      return "Kojin";

    if (npcData.Race == "Ananta")
      return "Ananta";

    if (npcData.Race == "Namazu")
      return "Namazu";

    if (npcData.Race == "Lupin")
    {
      if (speaker == "Hakuro" || speaker == "Hakuro Gunji" || speaker == "Hakuro Whitefang")
        return "Ranjit";

      int hashValue = speaker.GetHashCode();
      int result = (Math.Abs(hashValue) % 10) + 1;

      return result switch
      {
        1 => "Hrothgar_Helion_03",
        2 => "Hrothgar_Helion_04",
        3 => "Hrothgar_The_Lost_02",
        4 => "Hrothgar_The_Lost_03",
        5 => "Lalafell_Dunesfolk_Male_06",
        6 => "Roegadyn_Hellsguard_Male_04",
        7 => "Others_Widargelt",
        8 => "Hyur_Highlander_Male_04",
        9 => "Hrothgar_Helion_02",
        10 => "Hyur_Highlander_Male_05",
        _ => "Lupin",
      };
    }

    // Shb Beast Tribes
    if (npcData.Race == "Pixie")
      return "Pixie";

    // EW Beast Tribes
    if (npcData.Race == "Matanga")
    {
      if (npcData.Gender == "Female")
        return "Matanga_Female";
      else
        return "Matanga_Male";
    }

    if (npcData.Race == "Loporrit")
      return "Loporrit";

    if (npcData.Race == "Omicron")
      return "Omicron";

    if (npcData.Race == "Ea")
      return "Ea";

    // Bosses
    if (npcData.Race.StartsWith("Boss"))
      return npcData.Race;

    _logger.Debug("Cannot find a generic voice for " + speaker);
    return "Unknown";
  }

  private readonly Dictionary<int, string> _numberRomanDictionary = new()
  {
      { 1000, "M" },
      { 900, "CM" },
      { 500, "D" },
      { 400, "CD" },
      { 100, "C" },
      { 90, "XC" },
      { 50, "L" },
      { 40, "XL" },
      { 10, "X" },
      { 9, "IX" },
      { 5, "V" },
      { 4, "IV" },
      { 1, "I" },
    };

  private string RomanTo(int number)
  {
    StringBuilder roman = new();
    foreach (KeyValuePair<int, string> item in _numberRomanDictionary)
    {
      while (number >= item.Key)
      {
        roman.Append(item.Value);
        number -= item.Key;
      }
    }
    return roman.ToString();
  }

  private string ConvertRomanNumerals(string text)
  {
    string value = text;
    for (int i = 25; i > 5; i--)
    {
      string numeral = RomanTo(i);
      if (numeral.Length > 1)
      {
        value = value.Replace(numeral, i.ToString());
      }
    }
    return value;
  }
}
