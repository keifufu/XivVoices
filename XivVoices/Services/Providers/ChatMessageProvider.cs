using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace XivVoices.Services;

public interface IChatMessageProvider : IHostedService;

public class ChatMessageProvider(ILogger _logger, Configuration _configuration, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IGameInteropService _gameInteropService, IChatGui _chatGui, IObjectTable _objectTable) : IChatMessageProvider
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
    string speaker = "";
    string? speakerWorld = null;
    try
    {
      foreach (Payload item in message.Sender.Payloads)
      {
        if (item is PlayerPayload player)
        {
          speaker = player.PlayerName;
          speakerWorld = player.World.Value.Name.ToString();
          break;
        }

        // This should only be used for messages from the local player, as everyone else should always have a PlayerPayload.
        // So we will always set the world to our home world here.
        if (item is TextPayload text && text.Text != null && text.Text.ToString().Trim().Contains(' '))
        {
          speaker = text.Text;
          speakerWorld = _gameInteropService.PlayerWorld;
          break;
        }
      }
    }
    catch { }

    if (_selfTestService.Step == SelfTestStep.Provider_Chat)
      _selfTestService.Report_Provider_Chat(message.LogKind, speaker, speakerWorld, message.Message.ToString());

    bool allowed = false;
    switch (message.LogKind)
    {
      case XivChatType.Say:
        allowed = _configuration.ChatSayEnabled;
        break;
      case XivChatType.TellOutgoing:
        speaker = _gameInteropService.PlayerName ?? "Unknown";
        speakerWorld = _gameInteropService.PlayerWorld;
        allowed = _configuration.ChatTellEnabled;
        break;
      case XivChatType.TellIncoming:
        allowed = _configuration.ChatTellEnabled;
        break;
      case XivChatType.Shout:
      case XivChatType.Yell:
        allowed = _configuration.ChatShoutYellEnabled;
        break;
      case XivChatType.Party:
      case XivChatType.CrossParty:
        allowed = _configuration.ChatPartyEnabled;
        break;
      case XivChatType.Alliance:
        allowed = _configuration.ChatAllianceEnabled;
        break;
      case XivChatType.FreeCompany:
        allowed = _configuration.ChatFreeCompanyEnabled;
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
      case XivChatType.CustomEmote:
      case XivChatType.StandardEmote:
        allowed = _configuration.ChatEmotesEnabled;
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
      _ = _messageDispatcher.TryDispatch(MessageSource.ChatMessage, speaker, message.Message.ToString(), speakerWorld: speakerWorld);
    }
  }
}
