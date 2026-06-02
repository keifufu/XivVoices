using Microsoft.Extensions.Logging;
using ILogger = XivVoices.Services.ILogger;

#if !NO_KTK
using KamiToolKit;
#endif

namespace XivVoices;

public sealed class Plugin : IAsyncDalamudPlugin
{
  private readonly IDalamudPluginInterface _pluginInterface;
  private readonly IFramework _framework;
  private readonly IHost _host;

  public Plugin(
    IChatGui chatGui,
    IGameGui gameGui,
    IKeyState keyState,
    IToastGui toastGui,
    ICondition condition,
    IFramework framework,
    IPluginLog pluginLog,
    IClientState clientState,
    IDataManager dataManager,
    IObjectTable objectTable,
    IPlayerState playerState,
    IGamepadState gamepadState,
    ITargetManager targetManager,
    IAddonLifecycle addonLifecycle,
    IAgentLifecycle agentLifecycle,
    ICommandManager commandManager,
    ITextureProvider textureProvider,
    IAddonEventManager addonEventManager,
    IGameInteropProvider interopProvider,
    IDalamudPluginInterface pluginInterface,
    INotificationManager notificationManager
  )
  {
    _pluginInterface = pluginInterface;
    _framework = framework;

    _host = new HostBuilder()
      .UseContentRoot(pluginInterface.ConfigDirectory.FullName)
      .ConfigureLogging(lb =>
      {
        lb.ClearProviders();
        lb.SetMinimumLevel(LogLevel.Trace);
      })
      .ConfigureServices(collection =>
      {
        collection.AddSingleton(chatGui);
        collection.AddSingleton(gameGui);
        collection.AddSingleton(keyState);
        collection.AddSingleton(toastGui);
        collection.AddSingleton(condition);
        collection.AddSingleton(framework);
        collection.AddSingleton(pluginLog);
        collection.AddSingleton(clientState);
        collection.AddSingleton(dataManager);
        collection.AddSingleton(objectTable);
        collection.AddSingleton(playerState);
        collection.AddSingleton(gamepadState);
        collection.AddSingleton(targetManager);
        collection.AddSingleton(addonLifecycle);
        collection.AddSingleton(agentLifecycle);
        collection.AddSingleton(commandManager);
        collection.AddSingleton(textureProvider);
        collection.AddSingleton(interopProvider);
        collection.AddSingleton(pluginInterface);
        collection.AddSingleton(addonEventManager);
        collection.AddSingleton(notificationManager);

#if !NO_KTK
        collection.AddSingleton<ConfigAddon>();
        collection.AddSingleton<IOverlayAddon, OverlayAddon>();
#endif

        collection.AddSingleton<ConfigWindow>();
        collection.AddSingleton<ILogger, Logger>();
        collection.AddSingleton<ILipSync, LipSync>();
        collection.AddSingleton<IDataService, DataService>();
        collection.AddSingleton<ISoundFilter, SoundFilter>();
        collection.AddSingleton<IReportService, ReportService>();
        collection.AddSingleton<IWindowService, WindowService>();
        collection.AddSingleton<ICommandService, CommandService>();
        collection.AddSingleton<ILocalTTSService, LocalTTSService>();
        collection.AddSingleton<IPlaybackService, PlaybackService>();
        collection.AddSingleton<ISelfTestService, SelfTestService>();
        collection.AddSingleton<IAddonTalkProvider, AddonTalkProvider>();
        collection.AddSingleton<IMessageDispatcher, MessageDispatcher>();
        collection.AddSingleton<IClientStateService, ClientStateService>();
        collection.AddSingleton<IGameInteropService, GameInteropService>();
        collection.AddSingleton<IChatMessageProvider, ChatMessageProvider>();
        collection.AddSingleton<ISelectStringProvider, SelectStringProvider>();
        collection.AddSingleton<IAddonMiniTalkProvider, AddonMiniTalkProvider>();
        collection.AddSingleton<IAddonBattleTalkProvider, AddonBattleTalkProvider>();

        collection.AddSingleton(InitializeConfiguration);
        collection.AddSingleton(new WindowSystem(pluginInterface.InternalName));

        // The order of these matters, somewhat.
        collection.AddHostedService(sp => sp.GetRequiredService<ILogger>());
        collection.AddHostedService(sp => sp.GetRequiredService<IDataService>());
        collection.AddHostedService(sp => sp.GetRequiredService<ISoundFilter>());
        collection.AddHostedService(sp => sp.GetRequiredService<IWindowService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IReportService>());
        collection.AddHostedService(sp => sp.GetRequiredService<ICommandService>());
        collection.AddHostedService(sp => sp.GetRequiredService<ILocalTTSService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IPlaybackService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IMessageDispatcher>());
        collection.AddHostedService(sp => sp.GetRequiredService<IClientStateService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAddonTalkProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<IGameInteropService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IChatMessageProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<ISelectStringProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAddonMiniTalkProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAddonBattleTalkProvider>());

#if !NO_KTK
        collection.AddHostedService(sp => sp.GetRequiredService<IOverlayAddon>());
#endif
      }).Build();
  }

  private Configuration InitializeConfiguration(IServiceProvider s)
  {
    ILogger logger = s.GetRequiredService<ILogger>();
    IDalamudPluginInterface pluginInterface = s.GetRequiredService<IDalamudPluginInterface>();
    Configuration configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    configuration.Initialize(logger, pluginInterface);
    return configuration;
  }

  public async Task LoadAsync(CancellationToken token)
  {
#if !NO_KTK
    KamiToolKitLibrary.Initialize(_pluginInterface, _pluginInterface.InternalName);
#endif

    await _host.StartAsync(token);
  }

  public async ValueTask DisposeAsync()
  {
    await _host.StopAsync();
    _host.Dispose();

#if !NO_KTK
    await _framework.RunOnFrameworkThread(KamiToolKitLibrary.Dispose);
#endif
  }
}
