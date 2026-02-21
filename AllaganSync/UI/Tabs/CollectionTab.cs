using System;
using System.Numerics;
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

        if (ImGui.BeginTable("CollectionTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Collection", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Sync", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();

            foreach (var collector in syncService.Collectors)
            {
                var counts = syncService.GetCounts(collector.CollectionKey);
                string? warning = null;
                string? tooltip = null;

                if (collector.NeedsDataRequest && !collector.IsDataReady)
                {
                    warning = "Data not loaded";
                    tooltip = $"Open the {collector.DisplayName} window in-game to load the data.";
                }

                var enabled = charConfig.IsCollectionEnabled(collector.CollectionKey);
                if (DrawCollectionRow(collector.DisplayName, counts.unlocked, counts.total, enabled, out var newValue, warning, tooltip))
                {
                    charConfig.SetCollectionEnabled(collector.CollectionKey, newValue);
                    configService.Save();
                }
            }

            ImGui.EndTable();
        }
    }

    private static bool DrawCollectionRow(string name, int unlocked, int total, bool syncEnabled, out bool newValue, string? warning = null, string? tooltip = null)
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
