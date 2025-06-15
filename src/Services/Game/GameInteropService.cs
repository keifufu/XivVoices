using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace XivVoices.Services;

public interface IGameInteropService
{
  Task<IntPtr> TryFindCharacter(string name, uint? baseId);
  IntPtr TryFindCharacter_NoThreadCheck(string name, uint? baseId);
  unsafe NpcData? TryGetNpcDataFromCharacter(Character* character);
  Task<NpcData?> TryGetNpcData(string name, uint? baseId);
  bool IsInCutscene();
  bool IsInDuty();
}

public partial class GameInteropService(ICondition _condition, IFramework _framework, IClientState _clientState, IObjectTable _objectTable) : IGameInteropService
{
  public Task<IntPtr> TryFindCharacter(string name, uint? baseId) =>
    _framework.RunOnFrameworkThread(() => TryFindCharacter_NoThreadCheck(name, baseId));

  public unsafe IntPtr TryFindCharacter_NoThreadCheck(string name, uint? baseId)
  {
    IntPtr baseIdCharacter = IntPtr.Zero;

    foreach (IGameObject gameObject in _objectTable)
    {
      if ((gameObject as ICharacter) == null) continue;

      if (gameObject.DataId == baseId)
        baseIdCharacter = gameObject.Address;

      if (!string.IsNullOrEmpty(name) && gameObject.Name.TextValue == name)
        return gameObject.Address;
    }

    return baseIdCharacter;
  }

  private unsafe NpcData? TryGetNpcDataFromCharacter_Internal(Character* character)
  {
    if (character == null) return null;

    string speaker = character->GetName();

    byte[] customize = character->DrawData.CustomizeData.Data.ToArray();
    bool gender = Convert.ToBoolean(customize[(int)CustomizeIndex.Gender]);
    byte race = customize[(int)CustomizeIndex.Race];
    byte tribe = customize[(int)CustomizeIndex.Tribe];
    byte body = customize[(int)CustomizeIndex.ModelType];
    byte eyes = customize[(int)CustomizeIndex.EyeShape];

    NpcData npcData = new()
    {
      Gender = GetGender(gender),
      Race = GetRace(race),
      Tribe = GetTribe(tribe),
      Body = GetBody(body),
      Eyes = GetEyes(eyes),
      Type = GetBody(body) == "Elderly" ? "Old" : "Default",
      BaseId = character->BaseId
    };

    if (npcData.Body == "Beastman")
    {
      int skeletonId = character->ModelContainer.ModelSkeletonId;
      npcData.Race = GetSkeleton(skeletonId, _clientState.TerritoryType);

      // I would like examples for why these workarounds are necessary,
      // but as it stands this is copied from old XIVV
      if (speaker.Contains("Moogle"))
        npcData.Race = "Moogle";
    }

    return npcData;
  }

  public unsafe NpcData? TryGetNpcDataFromCharacter(Character* character) =>
    TryGetNpcDataFromCharacter_Internal(character);

  public unsafe Task<NpcData?> TryGetNpcData(string name, uint? baseId)
  {
    return _framework.RunOnFrameworkThread(() =>
    {
      Character* character = (Character*)TryFindCharacter_NoThreadCheck(name, baseId);
      return TryGetNpcDataFromCharacter_Internal(character);
    });
  }

  public bool IsInCutscene() =>
    _condition.Any(ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78);

  public bool IsInDuty() =>
    _condition.Any(ConditionFlag.BoundByDuty);
}
