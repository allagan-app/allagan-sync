using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AllaganSync.Services;
using AllaganSync.UI.Tabs;

namespace AllaganSync.UI;

public class MainWindow : Window, IDisposable
{
    private readonly InfoTab infoTab;
    private readonly CollectionTab collectionTab;
    private readonly EventsTab eventsTab;
#if DEBUG
    private readonly DebugTab debugTab;
#endif

    public MainWindow(ConfigurationService configService, AllaganSyncService syncService, AllaganApiClient apiClient, EventTrackingService eventTrackingService, Action? openSettings = null)
        : base("Allagan Sync###AllaganSyncMain")
    {
        infoTab = new InfoTab(configService, openSettings);
        collectionTab = new CollectionTab(configService, syncService, openSettings);
        eventsTab = new EventsTab(configService, eventTrackingService, openSettings);
#if DEBUG
        debugTab = new DebugTab(apiClient, configService);
#endif

        Size = new Vector2(500, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("MainTabs"))
        {
            if (ImGui.BeginTabItem("Info"))
            {
                infoTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Collection"))
            {
                collectionTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Events"))
            {
                eventsTab.Draw();
                ImGui.EndTabItem();
            }

#if DEBUG
            if (ImGui.BeginTabItem("Debug"))
            {
                debugTab.Draw();
                ImGui.EndTabItem();
            }
#endif

            ImGui.EndTabBar();
        }
    }

    public void Dispose() { }
}
