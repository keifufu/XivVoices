using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivVoices.Services;

public interface IGameInteropService
{
  Task<T> RunOnFrameworkThread<T>(Func<T> func);
  Task RunOnFrameworkThread(System.Action action);
  IGameObject? GetTarget();
  IntPtr TryFindCharacter(string name, uint? baseId);
  NpcEntry? TryGetNpc(string name, uint? baseId, NpcEntry? npc);
  string GetLocation();
  List<string> GetActiveQuests();
  List<string> GetActiveLeves();
  CameraView GetCameraView();
  bool IsInCutscene();
  bool IsInDuty();
  bool IsOccupiedBySummoningBell();
  string ReadUtf8String(Utf8String str);
  unsafe string ReadTextNode(AtkTextNode* textNode);
  unsafe (string race, string gender) GetBeastmanRace(Character* character);
}

public class CameraView
{
  public Vector3 Forward;
  public Vector3 Up;
  public Vector3 Right;
}

public partial class GameInteropService(ICondition _condition, IDataService _dataService, IFramework _framework, IClientState _clientState, IDataManager _dataManager, IObjectTable _objectTable, ITargetManager _targetManager) : IGameInteropService
{
  public Task<T> RunOnFrameworkThread<T>(Func<T> func) =>
    _framework.RunOnFrameworkThread(func);

  public Task RunOnFrameworkThread(System.Action action) =>
    _framework.RunOnFrameworkThread(action);

  public IGameObject? GetTarget()
    => _targetManager.Target;

  public IntPtr TryFindCharacter(string name, uint? baseId)
  {
    IntPtr baseIdCharacter = IntPtr.Zero;

    foreach (IGameObject gameObject in _objectTable)
    {
      if ((gameObject as ICharacter) == null) continue;

      if (gameObject.BaseId == baseId && baseId != 0)
        baseIdCharacter = gameObject.Address;

      if (!string.IsNullOrEmpty(name) && gameObject.Name.TextValue == name)
        return gameObject.Address;
    }

    return baseIdCharacter;
  }

  public unsafe NpcEntry? TryGetNpc(string name, uint? baseId, NpcEntry? _npc)
  {
    Character* character = (Character*)TryFindCharacter(name, baseId);
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
      Id = _npc?.Id,
      VoiceId = _npc?.VoiceId,
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
      (string b_race, string b_gender) = GetBeastmanRace(character);
      npc.Race = b_race;
      npc.Gender = b_gender;
      npc.Tribe = "";
      npc.Eyes = "";
    }

    return npc;
  }

  public string GetLocation()
  {
    string location = $"Unknown:{_clientState.TerritoryType}";
    if (_dataManager.GetExcelSheet<TerritoryType>().TryGetRow(_clientState.TerritoryType, out TerritoryType territory))
      location = $"{territory.PlaceName.Value.Name}";

    // If sheets have empty PlaceNames, revert to Unknown:<ID>
    if (location == "") location = $"Unknown:{_clientState.TerritoryType}";

    string coordinates;
    if (_objectTable.LocalPlayer != null)
    {
      Vector3 coordsVec3 = MapUtil.GetMapCoordinates(_objectTable.LocalPlayer);
      coordinates = $"({coordsVec3.X:F1}, {coordsVec3.Y:F1})";
    }
    else
    {
      coordinates = "(0, 0)";
    }

    return $"{location} {coordinates}";
  }

  public List<string> GetActiveQuests()
  {
    List<string> activeQuests = [];

    unsafe
    {
      foreach (QuestWork quest in QuestManager.Instance()->NormalQuests)
      {
        if (quest.QuestId is 0) continue;
        if (_dataManager.GetExcelSheet<Quest>().TryGetRow(quest.QuestId + 65536u, out Quest questData))
        {
          string name = Regex.Replace(questData.Name.ToString(), @"^[\uE000-\uF8FF]\s*", string.Empty);
          activeQuests.Add(name);
        }
      }
    }

    return activeQuests;
  }

  public List<string> GetActiveLeves()
  {
    List<string> activeLeves = [];

    unsafe
    {
      foreach (LeveWork leve in QuestManager.Instance()->LeveQuests)
      {
        if (leve.LeveId is 0) continue;
        if (_dataManager.GetExcelSheet<Leve>().TryGetRow(leve.LeveId, out Leve leveData))
        {
          string name = Regex.Replace(leveData.Name.ToString(), @"^[\uE000-\uF8FF]\s*", string.Empty);
          activeLeves.Add(name);
        }
      }
    }

    return activeLeves;
  }

  public unsafe CameraView GetCameraView()
  {
    Camera* camera = CameraManager.Instance()->GetActiveCamera();
    if (camera == null) return new();

    Matrix4x4 cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
    Vector3 cameraForward = Vector3.Normalize(new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33));
    Vector3 cameraUp = Vector3.Normalize(camera->CameraBase.SceneCamera.Vector_1);
    Vector3 cameraRight = Vector3.Normalize(Vector3.Cross(cameraUp, cameraForward));

    return new()
    {
      Forward = cameraForward,
      Up = cameraUp,
      Right = cameraRight
    };
  }

  public bool IsInCutscene() =>
    _condition.Any(ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78);

  public bool IsInDuty() =>
    _condition.Any(ConditionFlag.BoundByDuty);

  public bool IsOccupiedBySummoningBell() =>
    _condition.Any(ConditionFlag.OccupiedSummoningBell);

  public string ReadUtf8String(Utf8String str)
  {
    return new Lumina.Text.ReadOnly.ReadOnlySeString(str)
      .ToString()
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
