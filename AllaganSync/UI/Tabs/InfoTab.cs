using Dalamud.Bindings.ImGui;

namespace AllaganSync.UI.Tabs;

public static class InfoTab
{
    public static void Draw()
    {
        ImGui.Text("Getting Started");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped("Welcome to Allagan Sync! This plugin helps you track your collections and sync them with allagan.app.");

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextWrapped("To get started, set your API token in the 'API Token' tab.");
    }
}
