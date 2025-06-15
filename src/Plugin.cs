using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = XivVoices.Services.ILogger;

namespace XivVoices;

public sealed class Plugin : IDalamudPlugin
{
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
    IAddonLifecycle addonLifecycle,
    ICommandManager commandManager,
    ITextureProvider textureProvider,
    IGameInteropProvider interopProvider,
    IDalamudPluginInterface pluginInterface
  )
  {
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
        collection.AddSingleton(addonLifecycle);
        collection.AddSingleton(commandManager);
        collection.AddSingleton(textureProvider);
        collection.AddSingleton(interopProvider);
        collection.AddSingleton(pluginInterface);

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
        collection.AddSingleton<IAddonTalkProvider, AddonTalkProvider>();
        collection.AddSingleton<IMessageDispatcher, MessageDispatcher>();
        collection.AddSingleton<IAudioPostProcessor, AudioPostProcessor>();
        collection.AddSingleton<IGameInteropService, GameInteropService>();
        collection.AddSingleton<IChatMessageProvider, ChatMessageProvider>();
        collection.AddSingleton<IAddonMiniTalkProvider, AddonMiniTalkProvider>();
        collection.AddSingleton<IAddonBattleTalkProvider, AddonBattleTalkProvider>();

        collection.AddSingleton(InitializeConfiguration);
        collection.AddSingleton(new WindowSystem(pluginInterface.InternalName));

        // The order of these matters, somewhat.
        collection.AddHostedService(sp => sp.GetRequiredService<IDataService>());
        collection.AddHostedService(sp => sp.GetRequiredService<ISoundFilter>());
        collection.AddHostedService(sp => sp.GetRequiredService<IWindowService>());
        collection.AddHostedService(sp => sp.GetRequiredService<ICommandService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IReportService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IPlaybackService>());
        collection.AddHostedService(sp => sp.GetRequiredService<IMessageDispatcher>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAudioPostProcessor>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAddonTalkProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<IChatMessageProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAddonMiniTalkProvider>());
        collection.AddHostedService(sp => sp.GetRequiredService<IAddonBattleTalkProvider>());
      }).Build();

    _host.StartAsync();
  }

  private Configuration InitializeConfiguration(IServiceProvider s)
  {
    ILogger logger = s.GetRequiredService<ILogger>();
    IDalamudPluginInterface pluginInterface = s.GetRequiredService<IDalamudPluginInterface>();
    Configuration configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    configuration.Initialize(logger, pluginInterface);
    return configuration;
  }

  public void Dispose()
  {
    _host.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    _host.Dispose();
  }
}
