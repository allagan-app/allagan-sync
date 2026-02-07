using System.Numerics;
using Dalamud.Bindings.ImGui;
using AllaganSync.Services;

namespace AllaganSync.UI.Tabs;

public class CollectionTab
{
    private readonly ConfigurationService configService;
    private readonly AllaganSyncService syncService;
    private bool hasLoadedCounts;

    public CollectionTab(ConfigurationService configService, AllaganSyncService syncService)
    {
        this.configService = configService;
        this.syncService = syncService;
    }

    public void Draw()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        // Load counts once when tab is first drawn
        if (!hasLoadedCounts)
        {
            syncService.RefreshCounts();
            hasLoadedCounts = true;
        }

        DrawActionButtons(charConfig);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginTable("CollectionTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Collection", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Sync", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();

            // Orchestrions
            var orchCounts = syncService.OrchestrionCounts;
            if (DrawCollectionRow("Orchestrions", orchCounts.unlocked, orchCounts.total, charConfig.SyncOrchestrions, out var syncOrch))
            {
                charConfig.SyncOrchestrions = syncOrch;
                configService.Save();
            }

            // Emotes
            var emoteCounts = syncService.EmoteCounts;
            if (DrawCollectionRow("Emotes", emoteCounts.unlocked, emoteCounts.total, charConfig.SyncEmotes, out var syncEmotes))
            {
                charConfig.SyncEmotes = syncEmotes;
                configService.Save();
            }

            // Titles
            var titleCounts = syncService.TitleCounts;
            if (DrawCollectionRow("Titles", titleCounts.unlocked, titleCounts.total, charConfig.SyncTitles, out var syncTitles))
            {
                charConfig.SyncTitles = syncTitles;
                configService.Save();
            }

            // Mounts
            var mountCounts = syncService.MountCounts;
            if (DrawCollectionRow("Mounts", mountCounts.unlocked, mountCounts.total, charConfig.SyncMounts, out var syncMounts))
            {
                charConfig.SyncMounts = syncMounts;
                configService.Save();
            }

            // Minions
            var minionCounts = syncService.MinionCounts;
            if (DrawCollectionRow("Minions", minionCounts.unlocked, minionCounts.total, charConfig.SyncMinions, out var syncMinions))
            {
                charConfig.SyncMinions = syncMinions;
                configService.Save();
            }

            // Achievements
            var achievementCounts = syncService.AchievementCounts;
            if (DrawCollectionRow("Achievements", achievementCounts.unlocked, achievementCounts.total, charConfig.SyncAchievements, out var syncAchievements, !syncService.AchievementsLoaded ? "Open achievements menu to load" : null))
            {
                charConfig.SyncAchievements = syncAchievements;
                configService.Save();
            }

            // Bardings
            var bardingCounts = syncService.BardingCounts;
            if (DrawCollectionRow("Bardings", bardingCounts.unlocked, bardingCounts.total, charConfig.SyncBardings, out var syncBardings))
            {
                charConfig.SyncBardings = syncBardings;
                configService.Save();
            }

            // Triple Triad Cards
            var tripleTriadCardCounts = syncService.TripleTriadCardCounts;
            if (DrawCollectionRow("Triple Triad Cards", tripleTriadCardCounts.unlocked, tripleTriadCardCounts.total, charConfig.SyncTripleTriadCards, out var syncTripleTriadCards))
            {
                charConfig.SyncTripleTriadCards = syncTripleTriadCards;
                configService.Save();
            }

            // Fashion Accessories
            var fashionAccessoryCounts = syncService.FashionAccessoryCounts;
            if (DrawCollectionRow("Fashion Accessories", fashionAccessoryCounts.unlocked, fashionAccessoryCounts.total, charConfig.SyncFashionAccessories, out var syncFashionAccessories))
            {
                charConfig.SyncFashionAccessories = syncFashionAccessories;
                configService.Save();
            }

            // Facewear
            var facewearCounts = syncService.FacewearCounts;
            if (DrawCollectionRow("Facewear", facewearCounts.unlocked, facewearCounts.total, charConfig.SyncFacewear, out var syncFacewear))
            {
                charConfig.SyncFacewear = syncFacewear;
                configService.Save();
            }

            // Vistas
            var vistaCounts = syncService.VistaCounts;
            if (DrawCollectionRow("Vistas", vistaCounts.unlocked, vistaCounts.total, charConfig.SyncVistas, out var syncVistas))
            {
                charConfig.SyncVistas = syncVistas;
                configService.Save();
            }

            // Fish
            var fishCounts = syncService.FishCounts;
            if (DrawCollectionRow("Fish", fishCounts.unlocked, fishCounts.total, charConfig.SyncFish, out var syncFish))
            {
                charConfig.SyncFish = syncFish;
                configService.Save();
            }

            // Blue Mage Spells
            var blueMageSpellCounts = syncService.BlueMageSpellCounts;
            if (DrawCollectionRow("Blue Mage Spells", blueMageSpellCounts.unlocked, blueMageSpellCounts.total, charConfig.SyncBlueMageSpells, out var syncBlueMageSpells))
            {
                charConfig.SyncBlueMageSpells = syncBlueMageSpells;
                configService.Save();
            }

            // Character Customizations (Hairstyles, Face Paints)
            var characterCustomizationCounts = syncService.CharacterCustomizationCounts;
            if (DrawCollectionRow("Hairstyles & Face Paints", characterCustomizationCounts.unlocked, characterCustomizationCounts.total, charConfig.SyncCharacterCustomizations, out var syncCharacterCustomizations))
            {
                charConfig.SyncCharacterCustomizations = syncCharacterCustomizations;
                configService.Save();
            }

            ImGui.EndTable();
        }
    }

    private static bool DrawCollectionRow(string name, int unlocked, int total, bool syncEnabled, out bool newValue, string? warning = null)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Text(name);
        if (warning != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"({warning})");
        }

        ImGui.TableNextColumn();
        if (total > 0)
            ImGui.Text($"{unlocked}/{total}");
        else
            ImGui.TextDisabled("--/--");

        ImGui.TableNextColumn();
        newValue = syncEnabled;
        return ImGui.Checkbox($"##{name}", ref newValue);
    }

    private void DrawActionButtons(CharacterConfig charConfig)
    {
        if (syncService.IsRefreshing)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Refreshing...");
            ImGui.EndDisabled();
        }
        else
        {
            if (ImGui.Button("Refresh"))
            {
                syncService.RefreshCounts();
            }
        }

        ImGui.SameLine();

        if (!charConfig.HasApiToken)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Sync");
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Set API token first");
        }
        else if (syncService.IsSyncing)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Syncing...");
            ImGui.EndDisabled();
        }
        else
        {
            if (ImGui.Button("Sync"))
            {
                _ = syncService.SyncAsync();
            }
        }

        if (syncService.LastError != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), syncService.LastError);
        }
        else if (syncService.LastSyncTime != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.3f, 1, 0.3f, 1), $"Last sync: {syncService.LastSyncTime:HH:mm:ss}");
        }
    }
}
