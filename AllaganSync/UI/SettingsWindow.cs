using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AllaganSync.Services;
using AllaganSync.UI.Tabs;

namespace AllaganSync.UI;

public class SettingsWindow : Window
{
    private readonly SettingsTab settingsTab;

    public SettingsWindow(ConfigurationService configService)
        : base("Allagan Sync — Settings###AllaganSyncSettings")
    {
        settingsTab = new SettingsTab(configService);
        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() => settingsTab.Draw();
}
