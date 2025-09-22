using System.Security.Cryptography;

namespace XivVoices.Services;

public partial class MessageDispatcher
{
  private NpcEntry? GetNpcFromMappings(SpeakerMappingType type, string sentence)
  {
    if (_dataService.Manifest == null) return null;
    string sanitizedSentence = Regex.Replace(sentence, @"[^a-zA-Z]", "");

    if (_dataService.Manifest.SpeakerMappings[type].TryGetValue(sanitizedSentence, out string? npcId))
      if (npcId != null && _dataService.Manifest.Npcs.TryGetValue(npcId, out NpcEntry? npc))
        return npc;

    return null;
  }

  private NpcEntry? GetNpc(MessageSource source, string speaker)
  {
    if (source == MessageSource.ChatMessage) return null;
    if (_dataService.Manifest == null) return null;
    if (_dataService.Manifest.Npcs.TryGetValue(speaker, out NpcEntry? npc))
      return npc;
    return null;
  }

  private Task<(string id, string? path)> TryGetVoiceline(VoiceEntry? voice, NpcEntry? npc, string sentence)
  {
    return Task.Run(() =>
    {
      string voiceName = voice?.Name ?? "Unknown";
      string npcName = npc?.Name ?? "Unknown";
      string id = Md5(voiceName, npcName, sentence);

      _logger.Debug($"Searching for voiceline: ({voiceName}:{npcName}:{sentence}) ({id})");
      string voicelinePath = Path.Join(_dataService.VoicelinesDirectory, id + ".ogg");
      if (!File.Exists(voicelinePath))
      {
        _logger.Debug("Voiceline not found.");
        return (id, null);
      }

      _logger.Debug("Voiceline found.");
      return (id, (string?)voicelinePath);
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

  public bool SentenceHasPlayerName(string sentence, string? playerName)
  {
    if (playerName == null) return false;
    string[] fullname = playerName.Split(" ");
    if (sentence.Contains(fullname[0])) return true;
    if (sentence.Contains(fullname[1])) return true;
    return false;
  }

  // Sanitizes the speaker and sentence. This should preferably NEVER be changed,
  // as that would break a lot of voicelines generated before then.
  // If we do want to add something here, make SURE it will NOT affect existing lines.
  public (string speaker, string sentence) CleanMessage(string _speaker, string _sentence, string? playerName, bool legacyNameReplacement, bool keepName)
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
        if (playerName != null)
        {
          string[] fullname = playerName.Split(" ");
          if (legacyNameReplacement)
          {
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
          else
          {
            pattern = "\\b" + fullname[0] + "\\b";
            sentence = Regex.Replace(sentence, pattern, "_FIRSTNAME_");

            pattern = "\\b" + fullname[1] + "\\b";
            sentence = Regex.Replace(sentence, pattern, "_LASTNAME_");
          }
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

  // This should reasonably only be called for npcs with "HasVariedLooks" set to true.
  public VoiceEntry? GetGenericVoice(NpcEntry? npc)
  {
    if (_dataService.Manifest == null || npc == null) return null;

    // We match lazily for beastmen because old xivv npcdata cache
    // was very partial. Most beastman only have "Tribe" set.
    // This shouldn't matter for new npc entries.
    VoiceEntry? voice = null;
    if (npc.Body == "Beastman")
    {
      string key1 = npc.Race + npc.Gender;
      if (_dataService.Manifest.Npcs_Generic.TryGetValue(key1, out NpcEntry? _npc1))
        if (_npc1.VoiceId != null && _dataService.Manifest.Voices.TryGetValue(_npc1.VoiceId, out VoiceEntry? _voice1))
          voice = _voice1;

      string key2 = npc.Race;
      if (_dataService.Manifest.Npcs_Generic.TryGetValue(key2, out NpcEntry? _npc2))
        if (_npc2.VoiceId != null && _dataService.Manifest.Voices.TryGetValue(_npc2.VoiceId, out VoiceEntry? _voice2))
          voice = _voice2;
    }
    else
    {
      string key = npc.Gender + npc.Race + npc.Tribe + npc.Body + npc.Eyes;
      if (_dataService.Manifest.Npcs_Generic.TryGetValue(key, out NpcEntry? _npc))
        if (_npc.VoiceId != null && _dataService.Manifest.Voices.TryGetValue(_npc.VoiceId, out VoiceEntry? _voice))
          voice = _voice;
    }

    return voice;
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
