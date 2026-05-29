using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XivVoices.Services;

public interface ISelectStringProvider : IHostedService;

public class SelectStringProvider(ILogger _logger, Configuration _configuration, ISelfTestService _selfTestService, IMessageDispatcher _messageDispatcher, IGameInteropService _gameInteropService, IAddonLifecycle _addonLifecycle) : ISelectStringProvider
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "SelectString", OnSelectStringFinalize);
    _addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "CutSceneSelectString", OnCutSceneSelectStringFinalize);

    return _logger.ServiceLifecycle();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _addonLifecycle.UnregisterListener(OnSelectStringFinalize);
    _addonLifecycle.UnregisterListener(OnCutSceneSelectStringFinalize);

    return _logger.ServiceLifecycle();
  }

  private unsafe void OnSelectStringFinalize(AddonEvent type, AddonArgs args)
  {
    if (args.Addon.Address == 0) return;
    AddonSelectString* addon = (AddonSelectString*)args.Addon.Address;
    string title = addon->AtkValuesSpan[2].String.ToString();
    if (title == "What will you say?" || _gameInteropService.IsOccupiedInQuestEvent())
    {
      _logger.Debug($"Voicing SelectString: {title}:{_gameInteropService.IsOccupiedInQuestEvent()}");
      HandleSelectedString(addon->PopupMenu.PopupMenu.List);
    }
  }

  private unsafe void OnCutSceneSelectStringFinalize(AddonEvent type, AddonArgs args)
  {
    if (args.Addon.Address == 0) return;
    HandleSelectedString(((AddonCutSceneSelectString*)args.Addon.Address)->OptionList);
  }

  private unsafe void HandleSelectedString(AtkComponentList* list)
  {
    if (list is null) return;

    int selectedItem = list->SelectedItemIndex;
    if (selectedItem < 0 || selectedItem >= list->ListLength) return;

    AtkComponentListItemRenderer* listItemRenderer = list->ItemRendererList[selectedItem].AtkComponentListItemRenderer;
    if (listItemRenderer is null) return;

    AtkTextNode* buttonTextNode = listItemRenderer->AtkComponentButton.ButtonTextNode;
    if (buttonTextNode is null) return;

    string sentence = buttonTextNode->NodeText.AsReadOnlySeStringSpan().ExtractText();

    string? speaker = _gameInteropService.PlayerName;
    if (speaker == null) return;

    string? speakerWorld = _gameInteropService.PlayerWorld;
    if (speakerWorld == null) return;

    _selfTestService.Report_Provider_CutSceneSelectString(speaker, speakerWorld, sentence);

    if (_configuration.VoicePlayerChoices)
    {
      _logger.Debug($"speaker::{speaker}@{speakerWorld ?? "Unknown"} sentence::{sentence}");
      _messageDispatcher.TryDispatch(MessageSource.SelectString, speaker, sentence, speakerWorld: speakerWorld);
    }
  }
}
