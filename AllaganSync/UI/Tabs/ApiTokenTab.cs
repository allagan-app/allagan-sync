using Dalamud.Bindings.ImGui;
using AllaganSync.Services;

namespace AllaganSync.UI.Tabs;

public class ApiTokenTab
{
    private readonly ConfigurationService configService;

    private string tokenInput = string.Empty;
    private bool isEditingToken = false;

    public ApiTokenTab(ConfigurationService configService)
    {
        this.configService = configService;
    }

    public void Draw()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        ImGui.Text("API Token");
        ImGui.Separator();
        ImGui.Spacing();

        if (isEditingToken)
        {
            DrawEditMode(charConfig);
        }
        else
        {
            DrawViewMode(charConfig);
        }
    }

    private void DrawEditMode(CharacterConfig charConfig)
    {
        ImGui.SetNextItemWidth(300);
        ImGui.InputText("##token", ref tokenInput, 256);

        ImGui.SameLine();
        if (ImGui.Button("Save"))
        {
            charConfig.ApiToken = tokenInput;
            configService.Save();
            tokenInput = string.Empty;
            isEditingToken = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            tokenInput = string.Empty;
            isEditingToken = false;
        }
    }

    private void DrawViewMode(CharacterConfig charConfig)
    {
        if (charConfig.HasApiToken)
        {
            ImGui.Text("Token: ********");
            ImGui.SameLine();
            if (ImGui.Button("Change"))
            {
                isEditingToken = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                charConfig.ApiToken = string.Empty;
                configService.Save();
            }
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "No token configured");
            ImGui.SameLine();
            if (ImGui.Button("Set Token"))
            {
                isEditingToken = true;
            }
        }
    }
}
