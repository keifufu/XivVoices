using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivVoices.Services;

public interface IGameInteropService
{
  Task<IntPtr> TryFindCharacter(string name, uint? baseId);
  IntPtr TryFindCharacter_NoThreadCheck(string name, uint? baseId);
  Task<NpcEntry?> TryGetNpc(string name, uint? baseId, NpcEntry? npc);
  Task<bool> IsTargetingRetainerBell();
  bool IsInCutscene();
  bool IsInDuty();
  string ReadUtf8String(Utf8String str);
  unsafe string ReadTextNode(AtkTextNode* textNode);
}

public partial class GameInteropService(ICondition _condition, IFramework _framework, IClientState _clientState, IDataManager _dataManager, IObjectTable _objectTable, ITargetManager _targetManager) : IGameInteropService
{
  public Task<IntPtr> TryFindCharacter(string name, uint? baseId) =>
    _framework.RunOnFrameworkThread(() => TryFindCharacter_NoThreadCheck(name, baseId));

  public unsafe IntPtr TryFindCharacter_NoThreadCheck(string name, uint? baseId)
  {
    IntPtr baseIdCharacter = IntPtr.Zero;

    foreach (IGameObject gameObject in _objectTable)
    {
      if ((gameObject as ICharacter) == null) continue;

      if (gameObject.DataId == baseId && baseId != 0)
        baseIdCharacter = gameObject.Address;

      if (!string.IsNullOrEmpty(name) && gameObject.Name.TextValue == name)
        return gameObject.Address;
    }

    return baseIdCharacter;
  }

  private unsafe NpcEntry? TryGetNpcFromCharacter_Internal(Character* character, NpcEntry? _npc)
  {
    if (character == null) return null;

    string speaker = character->GetName();

    byte[] customize = character->DrawData.CustomizeData.Data.ToArray();
    bool gender = Convert.ToBoolean(customize[(int)CustomizeIndex.Gender]);
    byte race = customize[(int)CustomizeIndex.Race];
    byte tribe = customize[(int)CustomizeIndex.Tribe];
    byte body = customize[(int)CustomizeIndex.ModelType];
    byte eyes = customize[(int)CustomizeIndex.EyeShape];

    NpcEntry npc = new()
    {
      Id = _npc?.Id ?? "",
      Name = _npc?.Name ?? speaker,
      VoiceId = _npc?.VoiceId ?? "",
      Gender = GetGender(gender),
      Race = GetRace(race),
      Tribe = GetTribe(tribe),
      Body = GetBody(body),
      Eyes = GetEyes(eyes),
      BaseId = character->BaseId,
      Speakers = _npc?.Speakers ?? [speaker],
      HasVariedLooks = _npc?.HasVariedLooks ?? false
    };

    if (npc.Body == "Beastman")
    {
      int skeletonId = character->ModelContainer.ModelSkeletonId;
      npc.Race = GetSkeleton(skeletonId, _clientState.TerritoryType);

      // I would like examples for why these workarounds are necessary,
      // but as it stands this is copied from old XIVV
      if (speaker.Contains("Moogle"))
        npc.Race = "Moogle";
    }

    return npc;
  }

  public unsafe Task<NpcEntry?> TryGetNpc(string name, uint? baseId, NpcEntry? npc)
  {
    return _framework.RunOnFrameworkThread(() =>
    {
      Character* character = (Character*)TryFindCharacter_NoThreadCheck(name, baseId);
      return TryGetNpcFromCharacter_Internal(character, npc);
    });
  }

  internal string BellName => _dataManager.GetExcelSheet<EObjName>().GetRow(2000401).Singular.ExtractText();
  public Task<bool> IsTargetingRetainerBell()
  {
    return _framework.RunOnFrameworkThread(() =>
    {
      IGameObject? target = _targetManager.Target;
      if (target == null) return false;
      if (target.ObjectKind != ObjectKind.EventObj && target.ObjectKind != ObjectKind.Housing) return false;
      string name = target.Name.ToString();
      if (name.Equals(BellName, StringComparison.OrdinalIgnoreCase) || name.Equals("リテイナーベル")) return true;
      return false;
    });
  }

  public bool IsInCutscene() =>
    _condition.Any(ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78);

  public bool IsInDuty() =>
    _condition.Any(ConditionFlag.BoundByDuty);


  public string ReadUtf8String(Utf8String str)
  {
    return new Lumina.Text.ReadOnly.ReadOnlySeString(str)
      .ExtractText()
      .Trim()
      .Replace("\n", "")
      .Replace("\r", "");
  }

  public unsafe string ReadTextNode(AtkTextNode* textNode)
  {
    if (textNode == null) return "";
    return ReadUtf8String(textNode->NodeText);
  }
}
