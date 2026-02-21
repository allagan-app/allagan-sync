#if DEBUG
using System.Numerics;
using AllaganSync.Services;
using Dalamud.Bindings.ImGui;

namespace AllaganSync.UI.Tabs;

public class DebugTab
{
    private readonly AllaganApiClient apiClient;
    private readonly ConfigurationService configService;

    private string apiBaseUrlInput = string.Empty;
    private string apiTokenInput = string.Empty;
    private bool initialized = false;
    private bool confirmReset = false;

    public DebugTab(AllaganApiClient apiClient, ConfigurationService configService)
    {
        this.apiClient = apiClient;
        this.configService = configService;
    }

    public void Draw()
    {
        if (!initialized)
        {
            apiBaseUrlInput = configService.DebugBaseUrlOverride;
            apiTokenInput = configService.DebugTokenOverride;
            initialized = true;
        }

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Debug Build");
        ImGui.Spacing();

        var overridesEnabled = configService.DebugOverridesEnabled;
        if (ImGui.Checkbox("Enable Debug Overrides", ref overridesEnabled))
        {
            configService.DebugOverridesEnabled = overridesEnabled;
            configService.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled($"Active: {apiClient.BaseUrl}");

        ImGui.Spacing();

        if (!overridesEnabled)
            ImGui.BeginDisabled();

        ImGui.Text("API Base URL");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##apiBaseUrl", ref apiBaseUrlInput, 256);

        ImGui.Spacing();

        ImGui.Text("API Token Override");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##apiToken", ref apiTokenInput, 256);

        ImGui.Spacing();

        var isDirty = apiBaseUrlInput != configService.DebugBaseUrlOverride
                      || apiTokenInput != configService.DebugTokenOverride;
        if (!isDirty)
            ImGui.BeginDisabled();

        if (ImGui.Button("Apply"))
        {
            configService.DebugBaseUrlOverride = apiBaseUrlInput;
            configService.DebugTokenOverride = apiTokenInput;
            configService.Save();
        }

        if (!isDirty)
            ImGui.EndDisabled();

        if (!overridesEnabled)
            ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.Button("Reset Overrides"))
        {
            apiBaseUrlInput = string.Empty;
            apiTokenInput = string.Empty;
            configService.DebugOverridesEnabled = false;
            configService.DebugBaseUrlOverride = string.Empty;
            configService.DebugTokenOverride = string.Empty;
            configService.Save();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Plugin Configuration");
        ImGui.Spacing();

        if (!confirmReset)
        {
            if (ImGui.Button("Reset Plugin Config"))
                confirmReset = true;
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "This will delete all characters and tokens.");
            if (ImGui.Button("Confirm Reset"))
            {
                configService.Reset();
                apiBaseUrlInput = string.Empty;
                apiTokenInput = string.Empty;
                confirmReset = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                confirmReset = false;
        }
    }
}
#endif
