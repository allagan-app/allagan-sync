using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class SyncRequest
{
    [JsonPropertyName("orchestrions")]
    public List<uint> Orchestrions { get; set; } = new();

    [JsonPropertyName("emotes")]
    public List<uint> Emotes { get; set; } = new();

    [JsonPropertyName("titles")]
    public List<uint> Titles { get; set; } = new();

    [JsonPropertyName("mounts")]
    public List<uint> Mounts { get; set; } = new();

    [JsonPropertyName("minions")]
    public List<uint> Minions { get; set; } = new();

    [JsonPropertyName("achievements")]
    public List<uint> Achievements { get; set; } = new();

    [JsonPropertyName("bardings")]
    public List<uint> Bardings { get; set; } = new();

    [JsonPropertyName("triple_triad_cards")]
    public List<uint> TripleTriadCards { get; set; } = new();

    [JsonPropertyName("fashion_accessories")]
    public List<uint> FashionAccessories { get; set; } = new();

    [JsonPropertyName("facewears")]
    public List<uint> Facewear { get; set; } = new();

    [JsonPropertyName("vistas")]
    public List<uint> Vistas { get; set; } = new();

    [JsonPropertyName("fish")]
    public List<uint> Fish { get; set; } = new();

    [JsonPropertyName("blue_mage_spells")]
    public List<uint> BlueMageSpells { get; set; } = new();

    [JsonPropertyName("character_customizations")]
    public List<uint> CharacterCustomizations { get; set; } = new();
}

public class AllaganSyncService
{
    private readonly IPluginLog log;
    private readonly ConfigurationService configService;
    private readonly OrchestrionService orchestrionService;
    private readonly EmoteService emoteService;
    private readonly TitleService titleService;
    private readonly MountService mountService;
    private readonly MinionService minionService;
    private readonly AchievementService achievementService;
    private readonly BardingService bardingService;
    private readonly TripleTriadCardService tripleTriadCardService;
    private readonly FashionAccessoryService fashionAccessoryService;
    private readonly FacewearService facewearService;
    private readonly VistaService vistaService;
    private readonly FishService fishService;
    private readonly BlueMageSpellService blueMageSpellService;
    private readonly CharacterCustomizationService characterCustomizationService;
    private readonly HttpClient httpClient;

#if DEBUG
    private const string ApiBaseUrl = "http://allagan.test";
#else
    private const string ApiBaseUrl = "https://allagan.app";
#endif

    private const string SyncEndpoint = "/api/v1/character/collection/sync";

    public bool IsSyncing { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastSyncTime { get; private set; }

    // Cached counts - only refreshed manually
    public (int unlocked, int total) OrchestrionCounts { get; private set; }
    public (int unlocked, int total) EmoteCounts { get; private set; }
    public (int unlocked, int total) TitleCounts { get; private set; }
    public (int unlocked, int total) MountCounts { get; private set; }
    public (int unlocked, int total) MinionCounts { get; private set; }
    public (int unlocked, int total) AchievementCounts { get; private set; }
    public bool AchievementsLoaded => achievementService.IsLoaded;
    public (int unlocked, int total) BardingCounts { get; private set; }
    public (int unlocked, int total) TripleTriadCardCounts { get; private set; }
    public (int unlocked, int total) FashionAccessoryCounts { get; private set; }
    public (int unlocked, int total) FacewearCounts { get; private set; }
    public (int unlocked, int total) VistaCounts { get; private set; }
    public (int unlocked, int total) FishCounts { get; private set; }
    public (int unlocked, int total) BlueMageSpellCounts { get; private set; }
    public (int unlocked, int total) CharacterCustomizationCounts { get; private set; }
    public bool IsRefreshing { get; private set; }

    public void RefreshCounts()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        Task.Run(() =>
        {
            try
            {
                OrchestrionCounts = (orchestrionService.GetUnlockedIds().Count, orchestrionService.GetTotalCount());
                EmoteCounts = (emoteService.GetUnlockedIds().Count, emoteService.GetTotalCount());
                TitleCounts = (titleService.GetUnlockedIds().Count, titleService.GetTotalCount());
                MountCounts = (mountService.GetUnlockedIds().Count, mountService.GetTotalCount());
                MinionCounts = (minionService.GetUnlockedIds().Count, minionService.GetTotalCount());
                AchievementCounts = (achievementService.GetUnlockedIds().Count, achievementService.GetTotalCount());
                BardingCounts = (bardingService.GetUnlockedIds().Count, bardingService.GetTotalCount());
                TripleTriadCardCounts = (tripleTriadCardService.GetUnlockedIds().Count, tripleTriadCardService.GetTotalCount());
                FashionAccessoryCounts = (fashionAccessoryService.GetUnlockedIds().Count, fashionAccessoryService.GetTotalCount());
                FacewearCounts = (facewearService.GetUnlockedIds().Count, facewearService.GetTotalCount());
                VistaCounts = (vistaService.GetUnlockedIds().Count, vistaService.GetTotalCount());
                FishCounts = (fishService.GetUnlockedIds().Count, fishService.GetTotalCount());
                BlueMageSpellCounts = (blueMageSpellService.GetUnlockedIds().Count, blueMageSpellService.GetTotalCount());
                CharacterCustomizationCounts = (characterCustomizationService.GetUnlockedIds().Count, characterCustomizationService.GetTotalCount());
            }
            finally
            {
                IsRefreshing = false;
            }
        });
    }

    public AllaganSyncService(
        IPluginLog log,
        ConfigurationService configService,
        OrchestrionService orchestrionService,
        EmoteService emoteService,
        TitleService titleService,
        MountService mountService,
        MinionService minionService,
        AchievementService achievementService,
        BardingService bardingService,
        TripleTriadCardService tripleTriadCardService,
        FashionAccessoryService fashionAccessoryService,
        FacewearService facewearService,
        VistaService vistaService,
        FishService fishService,
        BlueMageSpellService blueMageSpellService,
        CharacterCustomizationService characterCustomizationService)
    {
        this.log = log;
        this.configService = configService;
        this.orchestrionService = orchestrionService;
        this.emoteService = emoteService;
        this.titleService = titleService;
        this.mountService = mountService;
        this.minionService = minionService;
        this.achievementService = achievementService;
        this.bardingService = bardingService;
        this.tripleTriadCardService = tripleTriadCardService;
        this.fashionAccessoryService = fashionAccessoryService;
        this.facewearService = facewearService;
        this.vistaService = vistaService;
        this.fishService = fishService;
        this.blueMageSpellService = blueMageSpellService;
        this.characterCustomizationService = characterCustomizationService;
        httpClient = new HttpClient();
    }

    public async Task<bool> SyncAsync()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
        {
            LastError = "No character logged in";
            return false;
        }

        if (!charConfig.HasApiToken)
        {
            LastError = "No API token configured";
            return false;
        }

        if (IsSyncing)
        {
            LastError = "Sync already in progress";
            return false;
        }

        IsSyncing = true;
        LastError = null;

        try
        {
            var request = await Task.Run(() =>
            {
                var req = new SyncRequest();

                if (charConfig.SyncOrchestrions)
                {
                    req.Orchestrions = orchestrionService.GetUnlockedIds();
                    log.Info($"Syncing {req.Orchestrions.Count} orchestrions");
                }

                if (charConfig.SyncEmotes)
                {
                    req.Emotes = emoteService.GetUnlockedIds();
                    log.Info($"Syncing {req.Emotes.Count} emotes");
                }

                if (charConfig.SyncTitles)
                {
                    req.Titles = titleService.GetUnlockedIds();
                    log.Info($"Syncing {req.Titles.Count} titles");
                }

                if (charConfig.SyncMounts)
                {
                    req.Mounts = mountService.GetUnlockedIds();
                    log.Info($"Syncing {req.Mounts.Count} mounts");
                }

                if (charConfig.SyncMinions)
                {
                    req.Minions = minionService.GetUnlockedIds();
                    log.Info($"Syncing {req.Minions.Count} minions");
                }

                if (charConfig.SyncAchievements && achievementService.IsLoaded)
                {
                    req.Achievements = achievementService.GetUnlockedIds();
                    log.Info($"Syncing {req.Achievements.Count} achievements");
                }

                if (charConfig.SyncBardings)
                {
                    req.Bardings = bardingService.GetUnlockedIds();
                    log.Info($"Syncing {req.Bardings.Count} bardings");
                }

                if (charConfig.SyncTripleTriadCards)
                {
                    req.TripleTriadCards = tripleTriadCardService.GetUnlockedIds();
                    log.Info($"Syncing {req.TripleTriadCards.Count} triple triad cards");
                }

                if (charConfig.SyncFashionAccessories)
                {
                    req.FashionAccessories = fashionAccessoryService.GetUnlockedIds();
                    log.Info($"Syncing {req.FashionAccessories.Count} fashion accessories");
                }

                if (charConfig.SyncFacewear)
                {
                    req.Facewear = facewearService.GetUnlockedIds();
                    log.Info($"Syncing {req.Facewear.Count} facewear");
                }

                if (charConfig.SyncVistas)
                {
                    req.Vistas = vistaService.GetUnlockedIds();
                    log.Info($"Syncing {req.Vistas.Count} vistas");
                }

                if (charConfig.SyncFish)
                {
                    req.Fish = fishService.GetUnlockedIds();
                    log.Info($"Syncing {req.Fish.Count} fish");
                }

                if (charConfig.SyncBlueMageSpells)
                {
                    req.BlueMageSpells = blueMageSpellService.GetUnlockedIds();
                    log.Info($"Syncing {req.BlueMageSpells.Count} blue mage spells");
                }

                if (charConfig.SyncCharacterCustomizations)
                {
                    req.CharacterCustomizations = characterCustomizationService.GetUnlockedIds();
                    log.Info($"Syncing {req.CharacterCustomizations.Count} character customizations");
                }

                return req;
            });

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{ApiBaseUrl}{SyncEndpoint}";

#if DEBUG
            log.Info($"curl -X POST '{url}' -H 'Content-Type: application/json' -H 'Authorization: Bearer {charConfig.ApiToken}' -d '{json}'");
#endif

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {charConfig.ApiToken}");

            var response = await httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"API error: {response.StatusCode}";
                log.Error($"Sync failed: {response.StatusCode}");
                return false;
            }

            LastSyncTime = DateTime.Now;
            log.Info("Sync completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Error: {ex.Message}";
            log.Error($"Sync exception: {ex}");
            return false;
        }
        finally
        {
            IsSyncing = false;
        }
    }
}
