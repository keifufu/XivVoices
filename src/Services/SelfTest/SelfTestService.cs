using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace XivVoices.Services;

public interface ISelfTestService
{
  SelfTestStep Step { get; }
  SelfTestStep SkippedTests { get; }
  SelfTestStep CompletedTests { get; }
  List<string> CurrentLogs { get; }
  string CurrentInstruction { get; }
  int StepState { get; }

  void Start();
  void Stop();
  void Next(bool completed, bool skipped, bool loop = false);
  void SkipTo(SelfTestStep state);

  void Report_SoundFilter_GetResourceSync(string path);
  void Report_SoundFilter_GetResourceAsync(string path);
  void Report_SoundFilter_LoadSoundFile(string name);
  void Report_SoundFilter_PlaySpecificSound(long a1, int idx);
  void Report_Provider_Chat(XivChatType type, string speaker, string sentence);
  void Report_Provider_Talk(string speaker, string sentence);
  unsafe void Report_Provider_MiniTalk(GameObject* actor, string sentence);
  void Report_Provider_BattleTalk(string speaker, string sentence);

  void LipSyncTarget();
}

[Flags]
public enum SelfTestStep : long
{
  None = 0,

  // Don't need to log in for these
  SoundFilter_GetResourceSync = 1L << 1,
  SoundFilter_GetResourceAsync = 1L << 2,
  SoundFilter_LoadSoundFile = 1L << 3,
  SoundFilter_PlaySpecificSound = 1L << 4,

  // Ul'dah plaza
  Provider_Chat = 1L << 5,
  Provider_Talk = 1L << 6,
  Provider_Talk_AutoAdvance = 1L << 7,
  Interop_GetNpcData = 1L << 8,
  LipSync = 1L << 9,

  // Right outside Ul'dah plaza
  Provider_MiniTalk = 1L << 10,
  Interop_GetLocation = 1L << 11,

  Interop_IsTargetingSummoningBell = 1L << 12,
  Interop_GetActiveQuests = 1L << 13,
  Interop_GetActiveLeves = 1L << 14,
  Interop_Camera = 1L << 15,

  Provider_BattleTalk = 1L << 16,
  Interop_IsInDuty = 1L << 17,
  Interop_IsInCutscene = 1L << 18,
}

public class SelfTestService(ILipSync _lipSync, IGameInteropService _gameInteropService, IClientState _clientState, IFramework _framework) : ISelfTestService
{
  public SelfTestStep Step { get; private set; } = SelfTestStep.None;
  public SelfTestStep SkippedTests { get; private set; } = SelfTestStep.None;
  public SelfTestStep CompletedTests { get; private set; } = SelfTestStep.None;
  public List<string> CurrentLogs { get; private set; } = [];
  public string CurrentInstruction { get; private set; } = "Press \"Start Self-Test\"";

  private readonly Dictionary<string, DateTime> _logTimestamps = [];
  private const int MaxLogs = 100;
  private const double LogDebounceTime = 3000;
  public int StepState { get; private set; } = 0;
  private int _questLeveState = -1;

  public void Start()
  {
    Next(false, false);
    SkippedTests = SelfTestStep.None;
    CompletedTests = SelfTestStep.None;

    _framework.Update += OnFrameworkUpdate;
  }

  public void Stop()
  {
    Step = SelfTestStep.None;
    CurrentInstruction = "Press \"Start Self-Test\"";

    _framework.Update -= OnFrameworkUpdate;
  }

  public void Next(bool completed, bool skipped, bool loop = false)
  {
    StepState = 0;
    _questLeveState = -1;

    if (completed)
      CompletedTests |= Step;

    bool alreadyCompleted = (CompletedTests & Step) == Step;
    if (skipped && !alreadyCompleted)
      SkippedTests |= Step;

    CurrentLogs = [];

    switch (Step)
    {
      case SelfTestStep.SoundFilter_GetResourceSync:
        CurrentInstruction = "Validate hook output";
        Step = SelfTestStep.SoundFilter_GetResourceAsync;
        break;
      case SelfTestStep.SoundFilter_GetResourceAsync:
        CurrentInstruction = "Validate hook output";
        Step = SelfTestStep.SoundFilter_LoadSoundFile;
        break;
      case SelfTestStep.SoundFilter_LoadSoundFile:
        CurrentInstruction = "Validate hook output";
        Step = SelfTestStep.SoundFilter_PlaySpecificSound;
        break;
      case SelfTestStep.SoundFilter_PlaySpecificSound:
        CurrentInstruction = "Send yourself a /tell with the sentence \"banana\"";
        Step = SelfTestStep.Provider_Chat;
        break;
      case SelfTestStep.Provider_Chat:
        CurrentInstruction = "Talk to Nenebaru in the Ul'dah plaza";
        Step = SelfTestStep.Provider_Talk;
        break;
      case SelfTestStep.Provider_Talk:
        CurrentInstruction = "Test if Auto-Advance is working.";
        Step = SelfTestStep.Provider_Talk_AutoAdvance;
        break;
      case SelfTestStep.Provider_Talk_AutoAdvance:
        CurrentInstruction = "If you see this, GetNpcData is not working.";
        Step = SelfTestStep.Interop_GetNpcData;
        break;
      case SelfTestStep.Interop_GetNpcData:
        CurrentInstruction = "Target Nenebaru and press \"LipSync Target\"";
        Step = SelfTestStep.LipSync;
        break;
      case SelfTestStep.LipSync:
        CurrentInstruction = "Walk outside of the plaza and trigger the guard's bubble.";
        Step = SelfTestStep.Provider_MiniTalk;
        break;
      case SelfTestStep.Provider_MiniTalk:
        CurrentInstruction = "Stand inside the guard";
        Step = SelfTestStep.Interop_GetLocation;
        break;
      case SelfTestStep.Interop_GetLocation:
        CurrentInstruction = "Target a Summoning Bell";
        Step = SelfTestStep.Interop_IsTargetingSummoningBell;
        break;
      case SelfTestStep.Interop_IsTargetingSummoningBell:
        CurrentInstruction = "Accept a Quest";
        Step = SelfTestStep.Interop_GetActiveQuests;
        break;
      case SelfTestStep.Interop_GetActiveQuests:
        CurrentInstruction = "Accept a Leve";
        Step = SelfTestStep.Interop_GetActiveLeves;
        break;
      case SelfTestStep.Interop_GetActiveLeves:
        CurrentInstruction = "Confirm Camera Logs";
        Step = SelfTestStep.Interop_Camera;
        break;
      case SelfTestStep.Interop_Camera:
        CurrentInstruction = "Join \"Hells Kier\" and attack Suzaku once.";
        Step = SelfTestStep.Provider_BattleTalk;
        break;
      case SelfTestStep.Provider_BattleTalk:
        CurrentInstruction = "Join any duty";
        Step = SelfTestStep.Interop_IsInDuty;
        break;
      case SelfTestStep.Interop_IsInDuty:
        CurrentInstruction = "Join any duty (again)";
        Step = SelfTestStep.Interop_IsInCutscene;
        break;
      case SelfTestStep.Interop_IsInCutscene:
        if (!loop)
        {
          Stop();
          return;
        }
        goto case SelfTestStep.None;
      case SelfTestStep.None:
        Step = SelfTestStep.SoundFilter_GetResourceSync; // Loop back to the start
        CurrentInstruction = "Validate hook output";
        break;
    }

    AddLog($"Started: {Step}");
  }

  public void SkipTo(SelfTestStep state)
  {
    if (Step == SelfTestStep.None)
      Start();

    while (Step != state)
      Next(false, true, true);
  }

  private void AddLog(string log)
  {
    DateTime now = DateTime.Now;
    if (_logTimestamps.TryGetValue(log, out DateTime lastLogged) && (now - lastLogged).TotalMilliseconds < LogDebounceTime)
      return;

    CurrentLogs.Insert(0, log);
    _logTimestamps[log] = now;

    if (CurrentLogs.Count > MaxLogs)
    {
      string oldestLog = CurrentLogs[^1];
      CurrentLogs.RemoveAt(CurrentLogs.Count - 1);
      _logTimestamps.Remove(oldestLog);
    }
  }

  private unsafe void OnFrameworkUpdate(IFramework _)
  {
    switch (Step)
    {
      case SelfTestStep.Interop_GetNpcData:
        NpcEntry? npc = _gameInteropService.TryGetNpc("Nenebaru", null, null);
        if (npc == null)
        {
          AddLog("Npc not found nearby");
          return;
        }

        bool passedChecks = true;
        void Check<T>(T a, T b, string name)
        {
          if (!EqualityComparer<T>.Default.Equals(a, b))
          {
            AddLog($"Unexpected Npc {name}: {a} (Expected {b})");
            passedChecks = false;
          }
        }

        Check(npc.Name, "Nenebaru", "Name");
        Check(npc.Gender, "Male", "Gender");
        Check(npc.Race, "Lalafell", "Race");
        Check(npc.Tribe, "Dunesfolk", "Tribe");
        Check(npc.Body, "Adult", "Body");
        Check(npc.Eyes, "Option 3", "Eyes");
        Check(npc.BaseId, 1001637U, "BaseId");

        if (passedChecks) Next(true, false);
        break;
      case SelfTestStep.Interop_GetLocation:
        string location = _gameInteropService.GetLocation();
        if (location == "Ul'dah - Steps of Nald (8.9, 8.4)")
          Next(true, false);
        else
          AddLog($"Unexpected location: {location}");
        break;
      case SelfTestStep.Interop_IsTargetingSummoningBell:
        switch (StepState)
        {
          case 0:
            if (_gameInteropService.IsTargetingSummoningBell())
            {
              StepState = 1;
              AddLog("Summoning Bell targeted");
              CurrentInstruction = "Stop targeting the Summining Bell";
            }
            break;
          case 1:
            if (!_gameInteropService.IsTargetingSummoningBell())
            {
              StepState = 2;
              AddLog("Summining Bell untargeted");
              CurrentInstruction = "Target the Summining Bell again";
            }
            break;
          case 2:
            if (_gameInteropService.IsTargetingSummoningBell())
              Next(true, false);
            break;
        }
        break;
      case SelfTestStep.Interop_GetActiveQuests:
        List<string> quests = _gameInteropService.GetActiveQuests();
        AddLog($"Active Quests: {string.Join(", ", quests)}");
        if (_questLeveState == -1) _questLeveState = quests.Count;
        switch (StepState)
        {
          case 0:
            if (quests.Count == _questLeveState + 1)
            {
              StepState = 1;
              _questLeveState = quests.Count;
              AddLog("Quest added.");
              CurrentInstruction = "Abandon the accepted Quest";
            }
            else
              AddLog($"Quest count remains the same ({quests.Count}/{_questLeveState}). Accept a Quest");
            break;
          case 1:
            if (quests.Count == _questLeveState - 1)
            {
              StepState = 2;
              AddLog("Quest abandoned.");
              CurrentInstruction = "Confirm the quest names were correct.";
            }
            else
              AddLog($"Quest count remains the same ({quests.Count}/{_questLeveState}). Abandon the Quest");
            break;
        }
        break;
      case SelfTestStep.Interop_GetActiveLeves:
        List<string> leves = _gameInteropService.GetActiveLeves();
        AddLog($"Active Leves: {string.Join(", ", leves)}");
        if (_questLeveState == -1) _questLeveState = leves.Count;
        switch (StepState)
        {
          case 0:
            if (leves.Count == _questLeveState + 1)
            {
              StepState = 1;
              _questLeveState = leves.Count;
              AddLog("Leve added.");
              CurrentInstruction = "Abandon the accepted Leve";
            }
            else
              AddLog($"Leve count remains the same ({leves.Count}/{_questLeveState}). Accept a Leve");
            break;
          case 1:
            if (leves.Count == _questLeveState - 1)
            {
              StepState = 2;
              AddLog("Leve abandoned.");
              CurrentInstruction = "Confirm the leve names were correct.";
            }
            else
              AddLog($"Leve count remains the same ({leves.Count}/{_questLeveState}). Abandon the Leve");
            break;
        }
        break;
      case SelfTestStep.Interop_Camera:
        CameraView cameraView = _gameInteropService.GetCameraView();
        AddLog($"F{cameraView.Forward:F1} U{cameraView.Up:F1} R:{cameraView.Right:F1}");
        break;
      case SelfTestStep.Interop_IsInDuty:
        switch (StepState)
        {
          case 0:
            if (_gameInteropService.IsInDuty())
            {
              StepState = 1;
              AddLog("Duty joined");
              CurrentInstruction = "Leave the duty";
            }
            break;
          case 1:
            if (!_gameInteropService.IsInDuty())
              Next(true, false);
            break;
        }
        break;
      case SelfTestStep.Interop_IsInCutscene:
        switch (StepState)
        {
          case 0:
            if (_gameInteropService.IsInCutscene())
            {
              StepState = 1;
              AddLog("Cutscene started");
              CurrentInstruction = "Stop watching the cutscene";
            }
            break;
          case 1:
            if (!_gameInteropService.IsInCutscene())
              Next(true, false);
            break;
        }
        break;
    }
  }

  public void Report_SoundFilter_GetResourceSync(string path)
    => AddLog(path);

  public void Report_SoundFilter_GetResourceAsync(string path)
    => AddLog(path);

  public void Report_SoundFilter_LoadSoundFile(string name)
    => AddLog(name);

  public void Report_SoundFilter_PlaySpecificSound(long a1, int idx)
    => AddLog($"{a1}:{idx}");

  public void Report_Provider_Chat(XivChatType type, string speaker, string sentence)
  {
    _framework.RunOnFrameworkThread(() =>
    {
      if (_clientState.LocalPlayer == null)
      {
        AddLog("LocalPlayer is null");
        return;
      }
      string playerName = _clientState.LocalPlayer.Name.ToString();

      switch (StepState)
      {
        case 0:
          if (type == XivChatType.TellOutgoing && speaker == playerName && sentence == "banana")
            StepState = 1;
          else
            AddLog($"Unexpected message: {type}, {speaker}, {sentence}");
          break;
        case 1:
          if (type == XivChatType.TellIncoming && speaker == playerName && sentence == "banana")
            Next(true, false);
          else
            AddLog($"Unexpected message: {type}, {speaker}, {sentence}");
          break;
      }
    });
  }

  public void Report_Provider_Talk(string speaker, string sentence)
  {
    if (speaker == "Nenebaru" && sentence == "Hail, adventurer! If you would know aught of aetherytes and aetherial travel, I, Nenebaru, would be happy to assist you.")
      Next(true, false);
    else
      AddLog($"Unexpected message: {speaker}, {sentence}");
  }

  public unsafe void Report_Provider_MiniTalk(GameObject* actor, string sentence)
  {
    if (actor->BaseId == 1001636U && sentence == "Plaza secure!")
      Next(true, false);
    else
      AddLog($"Unexpected message: {actor->BaseId}, {sentence}");
  }

  public void Report_Provider_BattleTalk(string speaker, string sentence)
  {
    if (speaker == "Suzaku" && sentence == "Tenzen? Tenzen, 'tis I! Sheathe your sword, I beg of you!")
      Next(true, false);
    else
      AddLog($"Unexpected message: {speaker}, {sentence}");
  }

  public void LipSyncTarget()
  {
    IGameObject? target = _gameInteropService.GetTarget();
    string targetName = target?.Name.ToString() ?? "";
    _lipSync.TryLipSync(new("_selfTestService.LipSyncTarget", MessageSource.AddonTalk, null, targetName, "", targetName, "", null, null), 10);
  }
}
