using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace XivVoices.Services;

public partial class GameInteropService
{
  private readonly Dictionary<int, string> _bodyMap = new()
  {
    {0, "Beastman"},
    {1, "Adult"},
    {3, "Elderly"},
    {4, "Child"},
  };

  private readonly Dictionary<int, string> _raceMap = new()
  {
    {1, "Hyur"},
    {2, "Elezen"},
    {3, "Lalafell"},
    {4, "Miqo'te"},
    {5, "Roegadyn"},
    {6, "Au Ra"},
    {7, "Hrothgar"},
    {8, "Viera"},
  };

  private readonly Dictionary<int, string> _tribeMap = new()
  {
    {1, "Midlander"},
    {2, "Highlander"},
    {3, "Wildwood"},
    {4, "Duskwight"},
    {5, "Plainsfolk"},
    {6, "Dunesfolk"},
    {7, "Seeker of the Sun"},
    {8, "Keeper of the Moon"},
    {9, "Sea Wolf"},
    {10, "Hellsguard"},
    {11, "Raen"},
    {12, "Xaela"},
    {13, "Helions"},
    {14, "The Lost"},
    {15, "Rava"},
    {16, "Veena"},
  };

  private readonly Dictionary<int, string> _eyesMap = new()
  {
    {0, "Option 1"},
    {1, "Option 2"},
    {2, "Option 3"},
    {3, "Option 4"},
    {4, "Option 5"},
    {5, "Option 6"},
    {128, "Option 1"},
    {129, "Option 2"},
    {130, "Option 3"},
    {131, "Option 4"},
    {132, "Option 5"},
    {133, "Option 6"},
  };

  private readonly Dictionary<(int, int), string> _beastRaceMap = new()
  {
    // A Realm Reborn
    {(11001, 3), "Amalj'aa"},
    {(11002, 4), "Ixal"},
    {(11003, 5), "Kobold"},
    {(11004, 6), "Goblin"},
    {(11005, 7), "Sylph"},
    {(11006, 8), "Moogle"},
    {(11007, 9), "Sahagin"},
    {(11012, 14), "Qiqirn"},
    {(11013, 15), "Moogle"}, // Post Moogle
    {(11008, 10), "Mamool Ja"},

    // Heavensward
    {(63, 0), "Dragon"},
    {(11020, 832), "Vath"},
    {(11001, 1101), "Vanu Vanu"}, // Also the same ids for Hanu Hanu (DT)
    {(278, 1111), "Allagan Node"},

    // Stormblood
    {(11028, 1699), "Kojin"},
    {(11012, 1726), "Qiqirn"}, // Ziggurat
    {(11029, 1740), "Ananta"},
    {(11030, 1812), "Lupin"},
    {(405, 1793), "Namazu"}, // Yanxia
    {(494, 2226), "Namazu"}, // Dhoro Iloh
    {(494, 2227), "Namazu"}, // Gyoshin
    {(495, 2228), "Namazu"}, // Seigetsu

    // Shadowbringers
    {(320, 2475), "Fuath"},
    {(11036, 2429), "Amaro"},
    {(11001, 2430), "Zun"},
    {(11003, 2518), "Mord"},
    {(11037, 2519), "Nu Mou"},
    {(11038, 2520), "Pixie"},
    {(11007, 2694), "Ondo"},

    // Endwalker
    {(706, 3135), "Ea"},
    {(11052, 3263), "Lopporit"},
    {(11051, 3264), "Omicron"},
    {(11055, 3293), "Matanga"},
    
    // Dawntrail
    {(11071, 4062), "Mamool Ja Male"},
    {(11072, 4064), "Mamool Ja Female"},
    {(11070, 4065), "Yok Huy"},
  };

  public string GetGender(bool id) => id ? "Female" : "Male";
  public string GetBody(int id) => _bodyMap.TryGetValue(id, out string? name) ? name : "Adult";
  public string GetRace(int id) => _raceMap.TryGetValue(id, out string? name) ? name : "Unknown:" + id.ToString();
  public string GetTribe(int id) => _tribeMap.TryGetValue(id, out string? name) ? name : "Unknown:" + id.ToString();
  public string GetEyes(int id) => _eyesMap.TryGetValue(id, out string? name) ? name : "Unknown:" + id.ToString();
  public unsafe string GetBeastmanRace(Character* character)
  {
    int modelSkeletonId = character->ModelContainer.ModelSkeletonId;
    int modelCharaId = character->ModelContainer.ModelCharaId;

    if (_beastRaceMap.TryGetValue((modelSkeletonId, modelCharaId), out string? race))
      return race;
    else if (_beastRaceMap.TryGetValue((modelSkeletonId, 0), out string? race2))
      return race2;

    return $"Unknown:{modelSkeletonId}:{modelCharaId}";
  }
}
