namespace XivVoices.Services;

public partial class LocalTTSService
{
  private async Task<string> ProcessPlayerChat(XivMessage message)
  {
    string sentence = message.Sentence.Trim();
    string playerName = message.OriginalSpeaker.Split(" ")[0];
    bool iAmSpeaking = await _framework.RunOnFrameworkThread(() => _clientState.LocalPlayer?.Name.TextValue == message.OriginalSpeaker);
    if (iAmSpeaking) playerName = "You";
    RegexOptions regexOptions = RegexOptions.IgnoreCase;

    // Regex: remove links
    sentence = Regex.Replace(sentence, @"https?\S*", "", regexOptions);

    // Regex: remove coordinates
    sentence = Regex.Replace(sentence, @"(\ue0bb[^\(]*?)\([^\)]*\)", "$1", regexOptions);

    // Check if the player is waving
    if (sentence.Equals("o/"))
    {
      if (iAmSpeaking)
        return playerName + " wave.";
      else
        return playerName + " is waving.";
    }

    if (_configuration.LocalTTSPlayerSays && !sentence.StartsWith(playerName))
    {
      string says = iAmSpeaking ? " say " : " says ";
      sentence = playerName + says + sentence;
    }

    // Replace "min" following numbers with "minutes", ensuring proper pluralization
    sentence = Regex.Replace(sentence, @"(\b\d+)\s*min\b", m =>
    {
      return int.Parse(m.Groups[1].Value) == 1 ? $"{m.Groups[1].Value} minute" : $"{m.Groups[1].Value} minutes";
    }, regexOptions);

    // Regex: replacements
    sentence = Regex.Replace(sentence, @"\bggty\b", "good game, thank you", regexOptions);
    sentence = Regex.Replace(sentence, @"\btyfp\b", "thank you for the party!", regexOptions);
    sentence = Regex.Replace(sentence, @"\bty4p\b", "thank you for the party!", regexOptions);
    sentence = Regex.Replace(sentence, @"\btyvm\b", "thank you very much", regexOptions);
    sentence = Regex.Replace(sentence, @"\btyft\b", "thank you for the train", regexOptions);
    sentence = Regex.Replace(sentence, @"\bty\b", "thank you", regexOptions);
    sentence = Regex.Replace(sentence, @"\brp\b", "role play", regexOptions);
    sentence = Regex.Replace(sentence, @"\bo7\b", "salute", regexOptions);
    sentence = Regex.Replace(sentence, @"\bafk\b", "away from keyboard", regexOptions);
    sentence = Regex.Replace(sentence, @"\bbrb\b", "be right back", regexOptions);
    sentence = Regex.Replace(sentence, @"\bprog\b", "progress", regexOptions);
    sentence = Regex.Replace(sentence, @"\bcomms\b", "commendations", regexOptions);
    sentence = Regex.Replace(sentence, @"\bcomm\b", "commendation", regexOptions);
    sentence = Regex.Replace(sentence, @"\blq\b", "low quality", regexOptions);
    sentence = Regex.Replace(sentence, @"\bhq\b", "high quality", regexOptions);
    sentence = Regex.Replace(sentence, @"\bfl\b", "friend list", regexOptions);
    sentence = Regex.Replace(sentence, @"\bfc\b", "free company", regexOptions);
    sentence = Regex.Replace(sentence, @"\bdot\b", "damage over time", regexOptions);
    sentence = Regex.Replace(sentence, @"\bcrit\b", "critical hit", regexOptions);
    sentence = Regex.Replace(sentence, @"\blol\b", "\"L-O-L\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\blmao\b", "\"Lah-mao\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\bgg\b", "good game", regexOptions);
    sentence = Regex.Replace(sentence, @"\bglhf\b", "good luck, have fun", regexOptions);
    sentence = Regex.Replace(sentence, @"\bgl\b", "good luck", regexOptions);
    sentence = Regex.Replace(sentence, @"\bsry\b", "sorry", regexOptions);
    sentence = Regex.Replace(sentence, @"\bsrry\b", "sorry", regexOptions);
    sentence = Regex.Replace(sentence, @"\bcs\b", "cutscene", regexOptions);
    sentence = Regex.Replace(sentence, @"\bttyl\b", "talk to you later", regexOptions);
    sentence = Regex.Replace(sentence, @"\boki\b", "okay", regexOptions);
    sentence = Regex.Replace(sentence, @"\bkk\b", "okay", regexOptions);
    sentence = Regex.Replace(sentence, @"\bffs\b", "for fuck's sake", regexOptions);
    sentence = Regex.Replace(sentence, @"\baight\b", "ight", regexOptions);
    sentence = Regex.Replace(sentence, @"\bggs\b", "good game", regexOptions);
    sentence = Regex.Replace(sentence, @"\bwp\b", "well played", regexOptions);
    sentence = Regex.Replace(sentence, @"\bgn\b", "good night", regexOptions);
    sentence = Regex.Replace(sentence, @"\bnn\b", "ight night", regexOptions);
    sentence = Regex.Replace(sentence, @"\bdd\b", "damage dealer", regexOptions);
    sentence = Regex.Replace(sentence, @"\bbis\b", "best in slot", regexOptions);
    sentence = Regex.Replace(sentence, @"(?<=\s|^):\)(?=\s|$)", "smile", regexOptions);
    sentence = Regex.Replace(sentence, @"(?<=\s|^):\((?=\s|$)", "sadge", regexOptions);
    sentence = Regex.Replace(sentence, @"\b<3\b", "heart", regexOptions);
    sentence = Regex.Replace(sentence, @"\bARR\b", "A Realm Reborn", regexOptions);
    sentence = Regex.Replace(sentence, @"\bHW\b", "Heavensward");
    sentence = Regex.Replace(sentence, @"\bSB\b", "Storm Blood");
    sentence = Regex.Replace(sentence, @"\bSHB\b", "Shadowbringers", regexOptions);
    sentence = Regex.Replace(sentence, @"\bEW\b", "End Walker");
    sentence = Regex.Replace(sentence, @"\bucob\b", "ultimate coils of bahamut", regexOptions);
    sentence = Regex.Replace(sentence, @"\bIT\b", "it");
    sentence = Regex.Replace(sentence, @"r says", "rr says");
    sentence = Regex.Replace(sentence, @"Eleanorr says", "el-uh-ner says");
    sentence = Regex.Replace(sentence, @"\bm1\b", "\"Melee one\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\bm2\b", "\"Melee two\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\bot\b", "\"Off-Tank\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\bMt\b", "\"Main-Tank\"");
    sentence = Regex.Replace(sentence, @"\bMT\b", "\"Main-Tank\"");
    sentence = Regex.Replace(sentence, @"\bmt\b", "\"mistake\"");
    sentence = Regex.Replace(sentence, @"\br1\b", "\"Ranged One\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\br2\b", "\"Ranged Two\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\bh1\b", "\"Healer One\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\bh2\b", "\"Healer Two\"", regexOptions);
    sentence = Regex.Replace(sentence, @"\brn\b", "\"right now\"", regexOptions);

    sentence = JobReplacement(sentence);
    return sentence;
  }

  private string JobReplacement(string sentence)
  {
    Dictionary<string, string> jobReplacementsCaseSensitive = new()
    {
      { "WAR", "Warrior" },
      { "SAM", "Samurai" }
    };

    Dictionary<string, string> jobReplacementsCaseInsensitive = new()
    {
      { "CRP", "Carpenter" },
      { "BSM", "Blacksmith" },
      { "ARM", "Armorer" },
      { "GSM", "Goldsmith" },
      { "LTW", "Leatherworker" },
      { "WVR", "Weaver" },
      { "ALC", "Alchemist" },
      { "CUL", "Culinarian" },
      { "MIN", "Miner" },
      { "BTN", "Botanist" },
      { "FSH", "Fisher" },
      { "GLA", "Gladiator" },
      { "PGL", "Pugilist" },
      { "MRD", "Marauder" },
      { "LNC", "Lancer" },
      { "ROG", "Rogue" },
      { "CNJ", "Conjurer" },
      { "THM", "Thaumaturge" },
      { "ACN", "Arcanist" },
      { "PLD", "Paladin" },
      { "DRK", "Dark Knight" },
      { "GNB", "Gunbreaker" },
      { "RPR", "Reaper" },
      { "MNK", "Monk" },
      { "DRG", "Dragoon" },
      { "NIN", "Ninja" },
      { "WHM", "White Mage" },
      { "SCH", "Scholar" },
      { "AST", "Astrologian" },
      { "SGE", "Sage" },
      { "BRD", "Bard" },
      { "MCH", "Machinist" },
      { "DNC", "Dancer" },
      { "BLM", "Black Mage" },
      { "SMN", "Summoner" },
      { "RDM", "Red Mage" },
      { "BLU", "Blue Mage" },
      { "PCT", "Pictohmanser" },
      { "VPR", "Viper" }
    };

    // Apply case-insensitive replacements for most job abbreviations
    foreach (KeyValuePair<string, string> job in jobReplacementsCaseInsensitive)
      sentence = Regex.Replace(sentence, $@"\b{job.Key}\b", job.Value, RegexOptions.IgnoreCase);

    // Apply case-sensitive replacements for "WAR," "ARC," and "SAM"
    foreach (KeyValuePair<string, string> job in jobReplacementsCaseSensitive)
      sentence = Regex.Replace(sentence, $@"\b{job.Key}\b", job.Value);

    return sentence;
  }

  private string ApplyLexicon(string sentence)
  {
    if (_dataService.Manifest == null) return sentence;

    string cleanedSentence = sentence;
    foreach (KeyValuePair<string, string> entry in _dataService.Manifest.Lexicon)
    {
      string pattern = "\\b" + entry.Key + "\\b";
      cleanedSentence = Regex.Replace(cleanedSentence, pattern, entry.Value, RegexOptions.IgnoreCase);
    }
    return cleanedSentence;
  }
}
