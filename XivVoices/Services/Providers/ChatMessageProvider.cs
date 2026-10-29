using Dalamud.Game.Chat;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace XivVoices.Services;

public interface IChatMessageProvider : IHostedService;

public class ChatMessageProvider(
  ILogger _logger,
  IChatGui _chatGui,
  IObjectTable _objectTable,
  IDataManager _dataManager,
  Configuration _configuration,
  ISelfTestService _selfTestService,
  IMessageDispatcher _messageDispatcher,
  IGameInteropService _gameInteropService) : IChatMessageProvider
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _chatGui.ChatMessage += OnChatMessage;

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _chatGui.ChatMessage -= OnChatMessage;

    return _logger.ServiceLifecycle();
  }

  private void OnChatMessage(IChatMessage message)
  {
    (string name, string world)? sender = GetSender(message);
    if (!sender.HasValue) return;
    string speaker = sender.Value.name;
    string speakerWorld = sender.Value.world;

    if (_selfTestService.Step == SelfTestStep.Provider_Chat)
      _selfTestService.Report_Provider_Chat(message.LogKind, speaker, speakerWorld, message.Message.ToString());

    bool allowed = false;
    switch (message.LogKind)
    {
      case XivChatType.Say:
        allowed = _configuration.ChatSayEnabled;
        break;
      case XivChatType.TellIncoming:
        allowed = _configuration.ChatTellEnabled;
        break;
      case XivChatType.TellOutgoing:
        speaker = _gameInteropService.PlayerName ?? "Unknown";
        speakerWorld = _gameInteropService.PlayerWorld ?? "Unknown";
        allowed = _configuration.ChatTellEnabled;
        break;
      case XivChatType.Party:
      case XivChatType.CrossParty:
        allowed = _configuration.ChatPartyEnabled;
        break;
      case XivChatType.Shout:
      case XivChatType.Yell:
        allowed = _configuration.ChatShoutYellEnabled;
        break;
      case XivChatType.Alliance:
        allowed = _configuration.ChatAllianceEnabled;
        break;
      case XivChatType.FreeCompany:
        allowed = _configuration.ChatFreeCompanyEnabled;
        break;
      case XivChatType.CustomEmote:
      case XivChatType.StandardEmote:
        allowed = _configuration.ChatEmotesEnabled;
        break;
      case XivChatType.CrossLinkShell1:
      case XivChatType.CrossLinkShell2:
      case XivChatType.CrossLinkShell3:
      case XivChatType.CrossLinkShell4:
      case XivChatType.CrossLinkShell5:
      case XivChatType.CrossLinkShell6:
      case XivChatType.CrossLinkShell7:
      case XivChatType.CrossLinkShell8:
      case XivChatType.Ls1:
      case XivChatType.Ls2:
      case XivChatType.Ls3:
      case XivChatType.Ls4:
      case XivChatType.Ls5:
      case XivChatType.Ls6:
      case XivChatType.Ls7:
      case XivChatType.Ls8:
        allowed = _configuration.ChatLinkshellEnabled;
        break;
    }

    if (_configuration.LocalTTSDisableLocalPlayerChat && _objectTable.LocalPlayer?.Name.ToString() == speaker)
      allowed = false;

    if (!_configuration.ChatEnabled)
      allowed = false;

    if (_configuration.LocalTTSIgnoreChatDuringCutscenes && _gameInteropService.IsInCutscene())
      allowed = false;

    if (allowed)
    {
      _logger.Debug($"speaker::{speaker}@{speakerWorld ?? "Unknown"} sentence::{message.Message}");
      _ = _messageDispatcher.TryDispatch(MessageSource.ChatMessage, speaker, message.Message.ToString(),
            speakerWorld: speakerWorld, chatChannel: message.LogKind);
    }
  }

  private (string, string)? GetSender(IChatMessage message)
  {
    foreach (ReadOnlySePayload payload in message.OriginalSender)
    {
      if (payload.Type != ReadOnlySePayloadType.Macro || payload.MacroCode != MacroCode.Link) continue;
      if (!payload.TryGetExpression(
        out ReadOnlySeExpression linkTypeExpression, out _,
        out ReadOnlySeExpression worldIdExpression, out _,
        out ReadOnlySeExpression characterNameExpression))
      {
        continue;
      }
      if (!linkTypeExpression.TryGetInt(out int payloadLinkType)) continue;
      if (payloadLinkType != (int)LinkMacroPayloadType.Character) continue;
      if (!worldIdExpression.TryGetUInt(out uint worldId)) continue;
      if (!characterNameExpression.TryGetString(out ReadOnlySeString characterName)) continue;
      if (!_dataManager.GetExcelSheet<World>().TryGetRow(worldId, out World world)) continue;
      return (characterName.ToString(), world.Name.ToString());
    }

    // Local player messages don't have a character payload.
    // If the sender otherwise matches the local player we will use that.
    if (_objectTable.LocalPlayer == null) return null;
    if (!message.OriginalSender.ToString().Contains(_objectTable.LocalPlayer.Name.ToString())) return null;
    return (_objectTable.LocalPlayer.Name.ToString(), _objectTable.LocalPlayer.HomeWorld.Value.Name.ToString());
  }
}
