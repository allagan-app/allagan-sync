using System;
using System.Linq;
using System.Numerics;
using AllaganSync.Collecting;
using AllaganSync.Collecting.Collectors;
using AllaganSync.Services;
using AllaganSync.UI;
using Dalamud.Bindings.ImGui;

namespace AllaganSync.UI.Tabs;

public class CollectionTab
{
    private readonly ConfigurationService configService;
    private readonly AllaganSyncService syncService;
    private readonly Action? openSettings;
    private bool hasLoadedCounts;

    public CollectionTab(ConfigurationService configService, AllaganSyncService syncService, Action? openSettings = null)
    {
        this.configService = configService;
        this.syncService = syncService;
        this.openSettings = openSettings;
    }

    public void Draw()
    {
        ImGui.TextWrapped("Shows your in-game unlock progress and syncs it with your allagan.app profile. " +
            "Toggle individual collections to control what gets synced.");
        ImGui.Spacing();
        ImGui.Spacing();

        if (!SetupGuard.Draw(configService, openSettings))
            return;

        var charConfig = configService.CurrentCharacter!;

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

        if (ImGui.BeginTable("CollectionTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Collection", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Sync", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("##Actions", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            foreach (var collector in syncService.Collectors)
            {
                if (collector is GearItemCollector gearCollector)
                {
                    DrawGearSection(gearCollector, charConfig);
                    continue;
                }

                var counts = syncService.GetCounts(collector.CollectionKey);
                string? warning = null;
                string? tooltip = null;
                var needsOpen = collector.NeedsDataRequest && !collector.IsDataReady;

                if (needsOpen)
                {
                    warning = "Data not loaded";
                    tooltip = $"Open the {collector.DisplayName} window in-game to load the data.";
                }

                var enabled = charConfig.IsCollectionEnabled(collector.CollectionKey);
                if (DrawCollectionRow(collector.DisplayName, counts.unlocked, counts.total, enabled, out var newValue, warning, tooltip, needsOpen ? collector.OpenGameUi : null))
                {
                    charConfig.SetCollectionEnabled(collector.CollectionKey, newValue);
                    configService.Save();
                }
            }

            ImGui.EndTable();
        }
    }

    private static bool DrawCollectionRow(string name, int unlocked, int total, bool syncEnabled, out bool newValue, string? warning = null, string? tooltip = null, Action? openGameUi = null)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Text(name);
        if (warning != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"({warning})");
            if (tooltip != null && ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        ImGui.TableNextColumn();
        if (total > 0)
            ImGui.Text($"{unlocked}/{total}");
        else
            ImGui.TextDisabled("--/--");

        ImGui.TableNextColumn();
        newValue = syncEnabled;
        var changed = ImGui.Checkbox($"##{name}", ref newValue);

        ImGui.TableNextColumn();
        if (openGameUi != null)
        {
            if (ImGui.SmallButton($"Open##{name}"))
                openGameUi();
        }

        return changed;
    }

    private void DrawGearSection(GearItemCollector gearCollector, CharacterConfig charConfig)
    {
        var counts = syncService.GetCounts(gearCollector.CollectionKey);
        var masterEnabled = charConfig.IsCollectionEnabled(gearCollector.CollectionKey);

        // Summary row
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var open = ImGui.TreeNode("Gear Items");

        ImGui.TableNextColumn();
        if (counts.total > 0)
            ImGui.Text($"{counts.unlocked}/{counts.total}");
        else
            ImGui.TextDisabled("--/--");

        ImGui.TableNextColumn();
        var newMasterValue = masterEnabled;
        if (ImGui.Checkbox("##GearItems", ref newMasterValue))
        {
            charConfig.SetCollectionEnabled(gearCollector.CollectionKey, newMasterValue);
            configService.Save();
        }

        ImGui.TableNextColumn(); // Actions column (empty for gear)

        if (open)
        {
            var sourceCounts = gearCollector.GetSourceCounts();

            // Separate live sources from retainer sources
            var liveSources = sourceCounts.Where(s => !s.Source.Key.StartsWith(InventorySource.RetainerKeyPrefix)).ToList();
            var retainerSources = sourceCounts.Where(s => s.Source.Key.StartsWith(InventorySource.RetainerKeyPrefix)).ToList();

            foreach (var (source, found, loaded) in liveSources)
            {
                DrawInventorySourceRow(gearCollector, source, found, loaded);
            }

            // Retainers group
            if (retainerSources.Count > 0)
            {
                var retainerTotal = retainerSources.Sum(r => r.Found);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"  Retainers");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "(cached)");

                ImGui.TableNextColumn();
                ImGui.Text($"{retainerTotal}");

                ImGui.TableNextColumn();
                var retainersEnabled = gearCollector.IsSourceEnabled("retainers");
                var newRetainersValue = retainersEnabled;
                if (ImGui.Checkbox("##retainers", ref newRetainersValue))
                {
                    gearCollector.SetSourceEnabled("retainers", newRetainersValue);
                }

                ImGui.TableNextColumn();

                foreach (var (source, found, _) in retainerSources)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text($"    {source.DisplayName}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{found}");

                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text("  Retainers");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "(no data)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Open a retainer's inventory to scan and cache their items.");

                ImGui.TableNextColumn();
                ImGui.TextDisabled("--");

                ImGui.TableNextColumn();
                var retainersEnabled = gearCollector.IsSourceEnabled("retainers");
                var newRetainersValue = retainersEnabled;
                if (ImGui.Checkbox("##retainers", ref newRetainersValue))
                {
                    gearCollector.SetSourceEnabled("retainers", newRetainersValue);
                }

                ImGui.TableNextColumn();
            }

            ImGui.TreePop();
        }
    }

    private static void DrawInventorySourceRow(GearItemCollector gearCollector, InventorySource source, int found, bool loaded)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Text($"  {source.DisplayName}");
        if (!loaded)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "(not loaded)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This inventory is not loaded yet. Open it in-game to load the data.");
        }

        ImGui.TableNextColumn();
        ImGui.Text($"{found}");

        ImGui.TableNextColumn();
        var sourceEnabled = gearCollector.IsSourceEnabled(source.Key);
        var newSourceValue = sourceEnabled;
        if (ImGui.Checkbox($"##{source.Key}", ref newSourceValue))
        {
            gearCollector.SetSourceEnabled(source.Key, newSourceValue);
        }

        ImGui.TableNextColumn();
        if (!loaded && source.OpenGameUi != null)
        {
            if (ImGui.SmallButton($"Open##{source.Key}"))
                source.OpenGameUi();
        }
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
