using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AllaganSync.Services;
using AllaganSync.UI.Tabs;

namespace AllaganSync.UI;

public class SettingsWindow : Window
{
    private readonly SettingsTab settingsTab;

    public SettingsWindow(ConfigurationService configService, EventTrackingService eventTrackingService)
        : base("Allagan Sync — Settings###AllaganSyncSettings")
    {
        settingsTab = new SettingsTab(configService, eventTrackingService);
        Size = new Vector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() => settingsTab.Draw();
}
