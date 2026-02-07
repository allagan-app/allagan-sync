using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using AllaganSync.Services;
using AllaganSync.UI;

namespace AllaganSync;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly TitleService titleService;
    private readonly AchievementService achievementService;
    private readonly WindowSystem windowSystem = new("AllaganSync");
    private readonly MainWindow mainWindow;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IPlayerState playerState,
        IDataManager dataManager,
        IClientState clientState,
        IUnlockState unlockState)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;
        this.clientState = clientState;

        var configService = new ConfigurationService(pluginInterface, playerState);
        var orchestrionService = new OrchestrionService(dataManager);
        var emoteService = new EmoteService(dataManager);
        titleService = new TitleService(dataManager, log);
        var mountService = new MountService(dataManager);
        var minionService = new MinionService(dataManager);
        achievementService = new AchievementService(dataManager, log);
        var bardingService = new BardingService(dataManager);
        var tripleTriadCardService = new TripleTriadCardService(dataManager);
        var fashionAccessoryService = new FashionAccessoryService(dataManager);
        var facewearService = new FacewearService(dataManager);
        var vistaService = new VistaService(dataManager);
        var fishService = new FishService(dataManager);
        var blueMageSpellService = new BlueMageSpellService(dataManager, unlockState);
        var characterCustomizationService = new CharacterCustomizationService(dataManager, unlockState);
        var syncService = new AllaganSyncService(log, configService, orchestrionService, emoteService, titleService, mountService, minionService, achievementService, bardingService, tripleTriadCardService, fashionAccessoryService, facewearService, vistaService, fishService, blueMageSpellService, characterCustomizationService);

        mainWindow = new MainWindow(configService, syncService);
        windowSystem.AddWindow(mainWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleMainWindow;
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;

        // Request data when character logs in
        clientState.Login += OnLogin;

        // If already logged in, request now
        if (clientState.IsLoggedIn)
            RequestData();

        log.Info("Allagan Sync loaded.");
    }

    private void OnLogin()
    {
        RequestData();
    }

    private void RequestData()
    {
        titleService.RequestTitleData();
        achievementService.RequestAchievementData();
    }

    public void ToggleMainWindow() => mainWindow.Toggle();

    public void Dispose()
    {
        clientState.Login -= OnLogin;

        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleMainWindow;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindow;

        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();

        log.Info("Allagan Sync unloaded.");
    }
}
