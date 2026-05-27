namespace XivVoices.Services;

public partial class LocalTTSService
{
  private Dictionary<int, char> _tokenToChar = [];
  private Dictionary<char, int> _vocab = [];
  private HashSet<int> _punctuationTokens = [];

  private readonly HashSet<char> _replaceablePhonemes = [.. "\n;:,.!?¡¿—…\"«»“”()"];
  private readonly HashSet<char> _spaceNeedingPhonemes = [.. "\"…<«“"];
  private readonly HashSet<char> _punctuation = [.. ";:,.!?…¿\n"];
  private readonly char[] _deletableCharacters = [.. "-`()[]{}"];

  private void InitializeTokenizer()
  {
    Dictionary<char, int> _vocabNew = new() { ['\n'] = -1, ['$'] = 0, [';'] = 1, [':'] = 2, [','] = 3, ['.'] = 4, ['!'] = 5, ['?'] = 6, ['¡'] = 7, ['¿'] = 8, ['—'] = 9, ['…'] = 10, ['\"'] = 11, ['('] = 12, [')'] = 13, ['“'] = 14, ['”'] = 15, [' '] = 16, ['\u0303'] = 17, ['ʣ'] = 18, ['ʥ'] = 19, ['ʦ'] = 20, ['ʨ'] = 21, ['ᵝ'] = 22, ['\uAB67'] = 23, ['A'] = 24, ['I'] = 25, ['O'] = 31, ['Q'] = 33, ['S'] = 35, ['T'] = 36, ['W'] = 39, ['Y'] = 41, ['ᵊ'] = 42, ['a'] = 43, ['b'] = 44, ['c'] = 45, ['d'] = 46, ['e'] = 47, ['f'] = 48, ['h'] = 50, ['i'] = 51, ['j'] = 52, ['k'] = 53, ['l'] = 54, ['m'] = 55, ['n'] = 56, ['o'] = 57, ['p'] = 58, ['q'] = 59, ['r'] = 60, ['s'] = 61, ['t'] = 62, ['u'] = 63, ['v'] = 64, ['w'] = 65, ['x'] = 66, ['y'] = 67, ['z'] = 68, ['ɑ'] = 69, ['ɐ'] = 70, ['ɒ'] = 71, ['æ'] = 72, ['β'] = 75, ['ɔ'] = 76, ['ɕ'] = 77, ['ç'] = 78, ['ɖ'] = 80, ['ð'] = 81, ['ʤ'] = 82, ['ə'] = 83, ['ɚ'] = 85, ['ɛ'] = 86, ['ɜ'] = 87, ['ɟ'] = 90, ['ɡ'] = 92, ['ɥ'] = 99, ['ɨ'] = 101, ['ɪ'] = 102, ['ʝ'] = 103, ['ɯ'] = 110, ['ɰ'] = 111, ['ŋ'] = 112, ['ɳ'] = 113, ['ɲ'] = 114, ['ɴ'] = 115, ['ø'] = 116, ['ɸ'] = 118, ['θ'] = 119, ['œ'] = 120, ['ɹ'] = 123, ['ɾ'] = 125, ['ɻ'] = 126, ['ʁ'] = 128, ['ɽ'] = 129, ['ʂ'] = 130, ['ʃ'] = 131, ['ʈ'] = 132, ['ʧ'] = 133, ['ʊ'] = 135, ['ʋ'] = 136, ['ʌ'] = 138, ['ɣ'] = 139, ['ɤ'] = 140, ['χ'] = 142, ['ʎ'] = 143, ['ʒ'] = 147, ['ʔ'] = 148, ['ˈ'] = 156, ['ˌ'] = 157, ['ː'] = 158, ['ʰ'] = 162, ['ʲ'] = 164, ['↓'] = 169, ['→'] = 171, ['↗'] = 172, ['↘'] = 173, ['ᵻ'] = 177 };

    (Dictionary<char, int>? c2t, Dictionary<int, char>? t2c) = ([], []);
    foreach ((char key, int val) in _vocabNew) { (c2t[key], t2c[val]) = (val, key); }
    (_vocab, _tokenToChar) = (c2t, t2c);
    _punctuationTokens = _punctuation.Select(x => _vocab[x]).ToHashSet();
  }

  public int[] Tokenize(string inputText)
  {
    string text = PreprocessText(inputText);
    string phonemes = Phonemize(CollectSymbols(text));
    return PostProcessPhonemes(text, phonemes.Split("\n")).Select(x => _vocab[x]).ToArray();
  }

  private string PreprocessText(string text)
  {
    text = text.Replace("\r\n", "\n");
    text = text.Replace("\r\n", "\n").Replace("**", "*").Replace("‘", "\"").Replace("’", "\"");
    text = text.Replace("{", ",").Replace("}", ",").Replace("(", ",").Replace(")", ",");

    foreach (char c in _deletableCharacters) { text = text.Replace(c.ToString(), " "); }
    foreach (char punc in _punctuation)
    {
      while (text.Contains($" {punc}")) { text = text.Replace($" {punc}", $"{punc}"); }
      text = text.Replace($"{punc}", $"{punc} ");
    }

    while (text.Length > 0 && _replaceablePhonemes.Contains(text[0]) || _deletableCharacters.Any(text.StartsWith)) { text = text[1..]; }
    while (text.Contains("\n\n")) { text = text.Replace("\n\n", "\n"); }
    for (int i = 0; i < 10; i++) { text = text.Replace("  ", " "); }

    return text.Trim();
  }

  private string CollectSymbols(string text)
  {
    text = text.Replace("\n", "\n ");
    foreach (char c in _replaceablePhonemes) { text = text.Replace(c, ','); }
    for (int i = 0; i < 10; i++) { text = text.Replace(" ,", ", "); }
    return text;
  }

  private string PostProcessPhonemes(string initialText, string[] phonemesArray)
  {
    List<string> puncs = [];
    for (int i = 0; i < initialText.Length; i++)
    {
      char c = initialText[i];
      if (_replaceablePhonemes.Contains(c))
      {
        string punc = c.ToString();
        while (i < initialText.Length - 1 && (_replaceablePhonemes.Contains(initialText[++i]) || initialText[i] == ' ')) { punc += initialText[i]; }
        puncs.Add(punc);
      }
    }

    StringBuilder sb = new();
    for (int i = 0; i < phonemesArray.Length; i++)
    {
      string vf = phonemesArray[i];
      if (vf.StartsWith("ˈɛ")) { vf = "ˌɛ" + vf[2..]; }
      sb.Append(vf);
      if (puncs.Count > i) { sb.Append(puncs[i]); }
    }
    string phonemes = sb.ToString().Trim();

    for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("  ", " "); }
    foreach (char f in _punctuation) { phonemes = phonemes.Replace($" {f}", f.ToString()); }
    for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("!!", "!").Replace("!?!", "!?"); }

    for (int i = 1; i < phonemes.Length - 1; i++)
    {
      if (!_spaceNeedingPhonemes.Contains(phonemes[i])) { continue; }
      if (phonemes[i - 1] != ' ')
      {
        char ph = phonemes[i];
        if (phonemes[i] == '"' && phonemes[i + 1] == ' ') { continue; }
        phonemes = phonemes.Insert(i, " ");
        i++;
      }
    }
    phonemes = phonemes.Replace("ː ", " ").Replace("ɔː", "ˌɔ").Replace("\n ", "\n");
    return new string(phonemes.Where(_vocab.ContainsKey).ToArray());
  }
}
