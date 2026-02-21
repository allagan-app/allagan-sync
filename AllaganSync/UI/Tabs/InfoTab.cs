using System;
using AllaganSync.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace AllaganSync.UI.Tabs;

public class InfoTab
{
    private const string AllaganUrl = "https://allagan.app";

    private readonly ConfigurationService configService;
    private readonly Action? openSettings;

    public InfoTab(ConfigurationService configService, Action? openSettings = null)
    {
        this.configService = configService;
        this.openSettings = openSettings;
    }

    public void Draw()
    {
        ImGui.Text("Allagan Sync");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped("This plugin syncs your in-game collections with allagan.app — " +
            "mounts, minions, orchestrion rolls, emotes, titles, achievements, bardings, " +
            "Triple Triad cards, fashion accessories, facewear, sightseeing log, fish, " +
            "Blue Mage spells, and character customizations.");

        ImGui.Spacing();
        ImGui.Spacing();

        if (!SetupGuard.Draw(configService, openSettings))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("You can generate a token on the website:");
            ImGui.Spacing();
            if (ImGui.SmallButton(AllaganUrl))
                Util.OpenLink(AllaganUrl);
            return;
        }

        ImGui.TextDisabled("You're all set! Use the Collection tab to sync your data.");
        ImGui.Spacing();
        if (ImGui.SmallButton(AllaganUrl))
            Util.OpenLink(AllaganUrl);
    }
}
