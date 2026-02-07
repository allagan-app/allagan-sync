using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AllaganSync.Services;
using AllaganSync.UI.Tabs;

namespace AllaganSync.UI;

public class MainWindow : Window, IDisposable
{
    private readonly ConfigurationService configService;
    private readonly CollectionTab collectionTab;
    private readonly ApiTokenTab apiTokenTab;

    public MainWindow(ConfigurationService configService, AllaganSyncService syncService)
        : base("Allagan Sync###AllaganSyncMain")
    {
        this.configService = configService;
        collectionTab = new CollectionTab(configService, syncService);
        apiTokenTab = new ApiTokenTab(configService);

        Size = new Vector2(500, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (!configService.IsLoggedIn)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Please log in to a character first.");
            return;
        }

        if (ImGui.BeginTabBar("MainTabs"))
        {
            if (ImGui.BeginTabItem("Info"))
            {
                InfoTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Collection"))
            {
                collectionTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("API Token"))
            {
                apiTokenTab.Draw();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public void Dispose() { }
}
