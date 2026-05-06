using System.Numerics;
using AllaganSync.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace AllaganSync.UI.Tabs;

public class SettingsTab
{
    private const string TokenUrl = "https://allagan.app/user/characters";
    private readonly ConfigurationService configService;

    private string tokenInput = string.Empty;
    private bool isEditingToken;

    public SettingsTab(ConfigurationService configService)
    {
        this.configService = configService;
    }

    public void Draw()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        DrawTokenSection(charConfig);
    }

    private void DrawTokenSection(CharacterConfig charConfig)
    {
        ImGui.Text("API Token");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextDisabled("Generate a token on the website:");
        ImGui.SameLine();
        if (ImGui.SmallButton("Open"))
            Util.OpenLink(TokenUrl);

        ImGui.Spacing();

        if (isEditingToken)
        {
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("##token", ref tokenInput, 256);

            ImGui.SameLine();
            if (ImGui.Button("Save"))
            {
                var trimmedToken = tokenInput.Trim();
                if (!string.IsNullOrEmpty(trimmedToken))
                {
                    charConfig.ApiToken = trimmedToken;
                    configService.Save();
                    tokenInput = string.Empty;
                    isEditingToken = false;
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                tokenInput = string.Empty;
                isEditingToken = false;
            }

            return;
        }

        if (charConfig.HasApiToken)
        {
            ImGui.Text("Token: ********");
            ImGui.SameLine();
            if (ImGui.Button("Change"))
                isEditingToken = true;
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                charConfig.ApiToken = string.Empty;
                configService.Save();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "No token configured");
            ImGui.SameLine();
            if (ImGui.Button("Set Token"))
                isEditingToken = true;
        }
    }
}
