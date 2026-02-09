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

  public string GetGender(bool id) => id ? "Female" : "Male";
  public string GetBody(int id) => _bodyMap.TryGetValue(id, out string? name) ? name : "Adult";
  public string GetRace(int id) => _raceMap.TryGetValue(id, out string? name) ? name : "Unknown:" + id.ToString();
  public string GetTribe(int id) => _tribeMap.TryGetValue(id, out string? name) ? name : "Unknown:" + id.ToString();
  public string GetEyes(int id) => _eyesMap.TryGetValue(id, out string? name) ? name : "Unknown:" + id.ToString();
  public unsafe (string race, string gender) GetBeastmanRace(Character* character)
  {
    int modelSkeletonId = character->ModelContainer.ModelSkeletonId;
    int modelCharaId = character->ModelContainer.ModelCharaId;

    if (_dataService.Manifest != null)
    {
      if (_dataService.Manifest.RaceMappings.TryGetValue((modelSkeletonId, modelCharaId), out var tuple))
        return tuple;
      else if (_dataService.Manifest.RaceMappings.TryGetValue((modelSkeletonId, 0), out var tuple2))
        return tuple2;
    }

    return ($"Unknown:{modelSkeletonId}:{modelCharaId}", "Male");
  }
}
