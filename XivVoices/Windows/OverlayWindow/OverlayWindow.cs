using Dalamud.Game.Addon.Events;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Overlay.UiOverlay;
using Microsoft.Extensions.DependencyInjection;

namespace XivVoices.Windows;

public interface IOverlayWindow : IHostedService
{
  public unsafe bool CheckCollision(AtkEventData* atkEventData);
}

public class OverlayWindow(ILogger _logger, Configuration _configuration, IFramework _framework, IClientState _clientState, IServiceProvider services) : IOverlayWindow
{
  private OverlayController? _overlayController;
  private XivvOverlayNode? _xivvOverlayNode;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _overlayController = new();

    _clientState.Login += RebuildOverlay;
    if (_clientState.IsLoggedIn) RebuildOverlay();

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _clientState.Login -= RebuildOverlay;

    _overlayController?.Dispose();
    _overlayController = null;

    _logger.ServiceLifecycle();
    return Task.CompletedTask;
  }

  private void RebuildOverlay() => _framework.RunOnFrameworkThread(() =>
  {
    _overlayController?.RemoveAllNodes();

    _xivvOverlayNode = new XivvOverlayNode(services)
    {
      Size = new(340.0f, 100.0f),
      Position = _configuration.OverlayPosition,
    };
    _overlayController?.AddNode(_xivvOverlayNode);
  });

  public unsafe bool CheckCollision(AtkEventData* atkEventData)
  {
    return _xivvOverlayNode?.Frame.CheckCollision(atkEventData) ?? false;
  }
}

public unsafe class XivvOverlayNode : OverlayNode
{
  public override OverlayLayer OverlayLayer => OverlayLayer.Foreground;
  public override bool HideWithNativeUi => false;

  private readonly IGameInteropService _gameInteropService;
  private readonly IAddonEventManager _addonEventManager;
  private readonly IMessageDispatcher _messageDispatcher;
  private readonly IPlaybackService _playbackService;
  private readonly Configuration _configuration;
  private readonly ConfigWindow _configWindow;
  private readonly ILogger _logger;

  public readonly WindowBackgroundNode Frame;
  private readonly ViewportEventListener _editEventListener;
  private readonly WindowBackgroundNode _frameFront;
  private readonly TextNode _titleText;
  private readonly CircleButtonNode _pinButton;
  private readonly CircleButtonNode _expandButton;
  private readonly CircleButtonNode _configButton;
  private readonly CircleButtonNode _closeButton;
  private readonly HorizontalLineNode _horizontalLine;
  private readonly TextButtonNode _pauseButton;
  private readonly CircleButtonNode _muteButton;
  private readonly SliderNode _volumeSlider;
  private readonly HorizontalListNode _horizontalList;
  private readonly HorizontalLineNode _horizontalLine2;
  private readonly TextNode _textNode;

  private Vector2 _clickStartPosition = Vector2.Zero;
  private bool _isCursorSet = false;
  private bool _isMoving = false;

  public XivvOverlayNode(IServiceProvider services)
  {
    _logger = services.GetRequiredService<ILogger>();
    _configWindow = services.GetRequiredService<ConfigWindow>();
    _configuration = services.GetRequiredService<Configuration>();
    _playbackService = services.GetRequiredService<IPlaybackService>();
    _messageDispatcher = services.GetRequiredService<IMessageDispatcher>();
    _addonEventManager = services.GetRequiredService<IAddonEventManager>();
    _gameInteropService = services.GetRequiredService<IGameInteropService>();

    _configuration.Saved += ConfigurationSaved;

    Frame = new WindowBackgroundNode(false)
    {
      Position = Vector2.Zero,
      Offsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
      NodeFlags = NodeFlags.Visible | NodeFlags.Fill,
      PartsRenderType = 19,
    };
    Frame.AttachNode(this);

    _editEventListener = new ViewportEventListener(OnEditEvent);
    _editEventListener.AddEvent(AtkEventType.MouseMove, Frame);
    _editEventListener.AddEvent(AtkEventType.MouseDown, Frame);

    _frameFront = new WindowBackgroundNode(true)
    {
      Position = Vector2.Zero,
      Offsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
      NodeFlags = NodeFlags.Visible | NodeFlags.Fill,
      PartsRenderType = 19,
    };
    _frameFront.AttachNode(this);

    _titleText = new TextNode
    {
      FontType = FontType.TrumpGothic,
      FontSize = 23,
      TextColor = ColorHelper.GetColor(50),
      TextOutlineColor = ColorHelper.GetColor(54),
      TextFlags = TextFlags.Edge,
      String = "XivVoices Overlay",
    };
    _titleText.AttachNode(this);

    _pinButton = new CircleButtonNode
    {
      Icon = ButtonIcon.Edit,
      OnClick = () =>
      {
        _configuration.OverlayPinned = !_configuration.OverlayPinned;
        _configuration.Save();
      }
    };
    _pinButton.AttachNode(this);

    _expandButton = new CircleButtonNode
    {
      Icon = _configuration.OverlayExpanded ? ButtonIcon.UpArrow : ButtonIcon.ArrowDown,
      OnClick = () =>
      {
        _configuration.OverlayExpanded = !_configuration.OverlayExpanded;
        _configuration.Save();
      },
    };
    _expandButton.AttachNode(this);

    _configButton = new CircleButtonNode
    {
      Icon = ButtonIcon.GearCog,
      TextTooltip = "Open Configuration",
      OnClick = () =>
      {
        _configWindow.SelectedTab = ConfigWindowTab.OverlaySettings;
        _configWindow.IsOpen = !_configWindow.IsOpen;
      },
    };
    _configButton.AttachNode(this);

    _closeButton = new CircleButtonNode
    {
      Icon = ButtonIcon.Cross,
      TextTooltip = "Close",
      OnClick = () =>
      {
        _configuration.OverlayOpen = false;
        _configuration.Save();
      },
    };
    _closeButton.AttachNode(this);

    _horizontalLine = new HorizontalLineNode();
    _horizontalLine.AttachNode(this);

    _pauseButton = new TextButtonNode
    {
      Height = 28.0f,
      Width = 60.0f,
      OnClick = () =>
      {
        _playbackService.Paused = !_playbackService.Paused;
      },
    };

    _muteButton = new CircleButtonNode
    {
      Width = 28.0f,
      Y = -2.0f,
      OnClick = () =>
      {
        _configuration.MuteEnabled = !_configuration.MuteEnabled;
        _configuration.Save();
        if (_configuration.MuteEnabled)
        {
          _messageDispatcher.ClearQueue();
          _playbackService.ClearQueue();
          _playbackService.StopAll();
        }
      }
    };

    _volumeSlider = new SliderNode
    {
      Y = 2.0f,
      Range = ..100,
      Width = 200.0f,
      IsEnabled = !_configuration.MuteEnabled,
      OnValueChanged = (value) =>
      {
        // If not for this check, we save the config way too much
        if (_configuration.Volume != value)
        {
          _configuration.Volume = value;
          _configuration.Save();
        }
      },
    };

    _horizontalList = new HorizontalListNode
    {
      Height = 28.0f,
      FitHeight = true,
      InitialNodes = [
        _pauseButton,
        new TextButtonNode {
          Height = 28.0f,
          Width = 50.0f,
          String = "Skip",
          OnClick = () => _playbackService.Skip(),
        },
        _muteButton,
        _volumeSlider
      ]
    };
    _horizontalList.AttachNode(this);

    _horizontalLine2 = new HorizontalLineNode
    {
      IsVisible = _configuration.OverlayExpanded
    };
    _horizontalLine2.AttachNode(this);

    _textNode = new TextNode
    {
      FontSize = 14,
      LineSpacing = 14,
      TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
      IsVisible = _configuration.OverlayExpanded,
      AlignmentType = AlignmentType.TopLeft
    };
    _textNode.AttachNode(this);

    ConfigurationSaved();
  }

  protected override void Dispose(bool disposing, bool isNativeDestructor)
  {
    base.Dispose(disposing, isNativeDestructor);

    _configuration.Saved -= ConfigurationSaved;

    _editEventListener.RemoveEvent(AtkEventType.MouseMove);
    _editEventListener.RemoveEvent(AtkEventType.MouseDown);
    _editEventListener.Dispose();
  }

  private void ConfigurationSaved()
  {
    Scale = new(_configuration.OverlayScale / 100.0f);
    _muteButton.Icon = _configuration.MuteEnabled ? ButtonIcon.Mute : ButtonIcon.Volume;

    _frameFront.IsVisible = _configuration.OverlayBorder;

    _volumeSlider.IsEnabled = !_configuration.MuteEnabled;
    _volumeSlider.Value = _configuration.Volume;

    _pinButton.TextTooltip = _configuration.OverlayPinned ? "Enable Moving" : "Disable Moving";
    _pinButton.AddColor = new(_configuration.OverlayPinned ? 0.0f : 0.2f);

    _expandButton.TextTooltip = _configuration.OverlayExpanded ? "Collapse" : "Expand";
    _expandButton.Icon = _configuration.OverlayExpanded ? ButtonIcon.UpArrow : ButtonIcon.ArrowDown;

    _horizontalLine2.IsVisible = _configuration.OverlayExpanded;
    _textNode.IsVisible = _configuration.OverlayExpanded;
  }

  protected override void OnUpdate()
  {
    IsVisible = _configuration.OverlayOpen && !(_configuration.OverlayHideInDuty && _gameInteropService.IsInDuty()) && !(_configuration.OverlayHideWhenMuted && _configuration.MuteEnabled);

    _pauseButton.String = _playbackService.Paused ? "Play" : "Pause";
    _pauseButton.AddColor = new(_playbackService.Paused ? 0.15f : 0.0f);

    XivMessage? message = _playbackService.GetLatestCurrentlyPlayingMessage();
    _textNode.String = message == null ? "Nothing is currently playing." : $"{message.RawSpeaker}: {message.AddName(message.RawSentence)}";
    Size = new(Size.X, _configuration.OverlayExpanded ? (_horizontalLine2.Bounds.Bottom + 20.0f + _textNode.GetTextDrawSize(false).Y) : 100.0f);
  }

  protected override void OnSizeChanged()
  {
    base.OnSizeChanged();

    Frame.Size = Size;
    _frameFront.Size = Size;

    _titleText.Size = new Vector2(Width - 8.0f, 31.0f);
    _titleText.Position = new Vector2(12.0f, 7.0f);

    _pinButton.Size = new Vector2(32.0f, 32.0f);
    _pinButton.Position = new Vector2(Width - 130, 7.0f);

    _expandButton.Size = new Vector2(32.0f, 32.0f);
    _expandButton.Position = new Vector2(Width - 100, 7.0f);

    _configButton.Size = new Vector2(32.0f, 32.0f);
    _configButton.Position = new Vector2(Width - 70.0f, 7.0f);

    _closeButton.Size = new Vector2(32.0f, 32.0f);
    _closeButton.Position = new Vector2(Width - 40.0f, 7.0f);

    _horizontalLine.Size = new Vector2(Width - 16.0f, 4.0f);
    _horizontalLine.Position = new Vector2(8.0f, _titleText.Bounds.Bottom + 2.0f);

    _horizontalList.Position = new Vector2(8.0f, _horizontalLine.Bounds.Bottom + 8.0f);

    _horizontalLine2.Size = new Vector2(Width - 16.0f, 4.0f);
    _horizontalLine2.Position = new Vector2(8.0f, _horizontalList.Bounds.Bottom + 2.0f);

    _textNode.Width = Width - 16.0f;
    _textNode.Position = new Vector2(8.0f, _horizontalLine2.Bounds.Bottom + 4.0f);
  }

  private void OnEditEvent(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
  {
    if (_configuration.OverlayPinned)
    {
      if (_isCursorSet) _addonEventManager.ResetCursor();
      return;
    }

    ref AtkEventData.AtkMouseData mouseData = ref atkEventData->MouseData;
    Vector2 mousePosition = new(mouseData.PosX, mouseData.PosY);
    Vector2 mouseDelta = mousePosition - _clickStartPosition;

    switch (eventType)
    {
      case AtkEventType.MouseMove when _isMoving:
        {
          Position += mouseDelta;
          _clickStartPosition = mousePosition;

          atkEvent->SetEventIsHandled(true);
        }
        break;

      case AtkEventType.MouseDown when Frame.CheckCollision(atkEventData) && !_isMoving:
        {
          _editEventListener.AddEvent(AtkEventType.MouseUp, Frame);

          _isMoving = true;
          _clickStartPosition = mousePosition;

          atkEvent->SetEventIsHandled(true);
        }
        break;

      case AtkEventType.MouseUp when _isMoving:
        {
          OnMoveComplete?.Invoke(this);
          OnEditComplete?.Invoke(this);

          _isMoving = false;
          _editEventListener.RemoveEvent(AtkEventType.MouseUp);

          _configuration.OverlayPosition = Position;
          _configuration.Save();
        }
        break;
    }

    if (_isCursorSet)
    {
      _addonEventManager.ResetCursor();
      _isCursorSet = false;
    }

    if (_isMoving)
    {
      _addonEventManager.SetCursor(AddonCursorType.Grab);
      _isCursorSet = true;
    }
    else if (CheckCollision(atkEventData))
    {
      _addonEventManager.SetCursor(AddonCursorType.Hand);
      _isCursorSet = true;
    }
  }
}
