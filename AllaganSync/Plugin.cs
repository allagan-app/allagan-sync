using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using AllaganSync.Collecting.Collectors;
using AllaganSync.Services;
using AllaganSync.Tracking.Trackers;
using AllaganSync.UI;

namespace AllaganSync;

public sealed class Plugin : IDalamudPlugin
{
    private const string MainCommand = "/allagansync";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly AllaganApiClient apiClient;
    private readonly AllaganSyncService syncService;
    private readonly EventTrackingService eventTrackingService;
    private readonly ContainerOpenTracker containerOpenTracker;
    private readonly WindowSystem windowSystem = new("AllaganSync");
    private readonly MainWindow mainWindow;
    private readonly SettingsWindow settingsWindow;
    private ulong lastContentId;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IPlayerState playerState,
        IDataManager dataManager,
        IClientState clientState,
        IFramework framework,
        IGameInventory gameInventory,
        IUnlockState unlockState,
        IGameInteropProvider gameInteropProvider)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        this.playerState = playerState;
        this.clientState = clientState;
        this.framework = framework;
        lastContentId = playerState.ContentId;

        var configService = new ConfigurationService(pluginInterface, playerState);
        apiClient = new AllaganApiClient(log, configService);
        syncService = new AllaganSyncService(log, configService, apiClient);

        // Register collectors (order determines UI display order)
        syncService.RegisterCollector(new OrchestrionCollector(dataManager, unlockState));
        syncService.RegisterCollector(new EmoteCollector(dataManager, unlockState, playerState));
        syncService.RegisterCollector(new TitleCollector(dataManager, log));
        syncService.RegisterCollector(new MountCollector(dataManager, unlockState));
        syncService.RegisterCollector(new MinionCollector(dataManager, unlockState));
        syncService.RegisterCollector(new AchievementCollector(dataManager, log));
        syncService.RegisterCollector(new BardingCollector(dataManager, unlockState));
        syncService.RegisterCollector(new TripleTriadCardCollector(dataManager, unlockState));
        syncService.RegisterCollector(new FashionAccessoryCollector(dataManager, unlockState));
        syncService.RegisterCollector(new FacewearCollector(dataManager, unlockState));
        syncService.RegisterCollector(new VistaCollector(dataManager));
        syncService.RegisterCollector(new FishCollector(dataManager));
        syncService.RegisterCollector(new BlueMageSpellCollector(dataManager, unlockState));
        syncService.RegisterCollector(new CharacterCustomizationCollector(dataManager, unlockState));

        // Event tracking
        eventTrackingService = new EventTrackingService(log, configService, apiClient);
        var desynthTracker = new DesynthTracker(log, dataManager, gameInteropProvider);
        eventTrackingService.RegisterTracker(desynthTracker);
        var retainerMissionTracker = new RetainerMissionTracker(log, dataManager, gameInteropProvider);
        eventTrackingService.RegisterTracker(retainerMissionTracker);
        var monsterSpawnTracker = new MonsterSpawnTracker(log, clientState, gameInteropProvider);
        eventTrackingService.RegisterTracker(monsterSpawnTracker);
        containerOpenTracker = new ContainerOpenTracker(log, gameInventory, framework, apiClient);
        eventTrackingService.RegisterTracker(containerOpenTracker);
        eventTrackingService.UpdateTrackerStates();

        if (clientState.IsLoggedIn)
            eventTrackingService.Start();

        settingsWindow = new SettingsWindow(configService, eventTrackingService);
        mainWindow = new MainWindow(configService, syncService, apiClient, eventTrackingService, () => settingsWindow.IsOpen = true);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(settingsWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleSettingsWindow;
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;
        framework.Update += OnFrameworkUpdate;
        commandManager.AddHandler(MainCommand, new CommandInfo(OnMainCommand)
        {
            HelpMessage = "Open the Allagan Sync window."
        });

        // Request data when character logs in
        clientState.Login += OnLogin;
        clientState.Logout += OnLogout;

        // If already logged in, request now
        if (clientState.IsLoggedIn)
        {
            syncService.RequestData();
            _ = containerOpenTracker.LoadContainerListAsync();
            _ = eventTrackingService.LoadAbilitiesAsync().ContinueWith(_ => eventTrackingService.UpdateTrackerStates());
        }

        log.Info("Allagan Sync loaded.");
    }

    private void RefreshDisplayData()
    {
        syncService.RequestData();
        syncService.RefreshCounts();
    }

    private void OnCharacterChanged()
    {
        RefreshDisplayData();
        eventTrackingService.UpdateTrackerStates();
    }

    private void OnLogin()
    {
        _ = OnLoginAsync();
    }

    private async Task OnLoginAsync()
    {
        await eventTrackingService.LoadAbilitiesAsync();
        OnCharacterChanged();
        eventTrackingService.Start();
        _ = containerOpenTracker.LoadContainerListAsync();
    }

    private void OnLogout(int type, int code)
    {
        containerOpenTracker.FlushAndClear();
        _ = eventTrackingService.FlushAsync();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var currentContentId = playerState.ContentId;
        if (currentContentId == lastContentId)
            return;

        lastContentId = currentContentId;
        if (currentContentId != 0)
            OnCharacterChanged();
    }

    public void ToggleMainWindow()
    {
        var wasOpen = mainWindow.IsOpen;
        mainWindow.Toggle();

        if (!wasOpen && mainWindow.IsOpen)
            RefreshDisplayData();
    }

    private void ToggleSettingsWindow()
    {
        settingsWindow.Toggle();
    }

    private void OnMainCommand(string command, string args)
    {
        OpenMainWindow();
    }

    private void OpenMainWindow()
    {
        if (mainWindow.IsOpen)
            return;

        mainWindow.IsOpen = true;
        RefreshDisplayData();
    }

    public void Dispose()
    {
        clientState.Login -= OnLogin;
        clientState.Logout -= OnLogout;

        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleSettingsWindow;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindow;
        framework.Update -= OnFrameworkUpdate;
        commandManager.RemoveHandler(MainCommand);

        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        eventTrackingService.Dispose();
        apiClient.Dispose();

        log.Info("Allagan Sync unloaded.");
    }
}
