using System.Numerics;
using AllaganSync.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace AllaganSync.UI.Tabs;

public class SettingsTab
{
    private const string TokenUrl = "https://allagan.app/user/characters";
    private readonly ConfigurationService configService;
    private readonly EventTrackingService eventTrackingService;

    private string tokenInput = string.Empty;
    private bool isEditingToken = false;

    public SettingsTab(ConfigurationService configService, EventTrackingService eventTrackingService)
    {
        this.configService = configService;
        this.eventTrackingService = eventTrackingService;
    }

    public void Draw()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        DrawTokenSection(charConfig);

        ImGui.Spacing();
        ImGui.Spacing();

        DrawTrackingSection(charConfig);
    }

    // ── Token Management ──────────────────────────────────────────────

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
        }
        else
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
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "No token configured");
                ImGui.SameLine();
                if (ImGui.Button("Set Token"))
                {
                    isEditingToken = true;
                }
            }
        }
    }

    // ── Event Tracking ────────────────────────────────────────────────

    private void DrawTrackingSection(CharacterConfig charConfig)
    {
        ImGui.Text("Event Tracking");
        ImGui.Separator();
        ImGui.Spacing();

        if (!charConfig.HasApiToken)
        {
            ImGui.BeginDisabled();
        }

        ImGui.TextWrapped("When enabled, the plugin automatically captures certain in-game activities " +
            "such as desynthesis results and contributes them to Allagan's community-driven " +
            "statistics like drop rates. Your data is only used anonymously in aggregate statistics.");
        ImGui.Spacing();

        var trackingEnabled = charConfig.TrackingEnabled;
        if (ImGui.Checkbox("Enable Event Tracking", ref trackingEnabled))
        {
            charConfig.TrackingEnabled = trackingEnabled;
            configService.Save();

            if (trackingEnabled)
                eventTrackingService.Start();
            else
                eventTrackingService.Stop();

            eventTrackingService.UpdateTrackerStates();
        }

        if (!charConfig.HasApiToken)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "(requires API token)");
        }

        if (charConfig.TrackingEnabled && charConfig.HasApiToken)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Tracked Events");
            ImGui.Indent();

            foreach (var tracker in eventTrackingService.Trackers)
            {
                if (tracker.RequiredAbility != null && !eventTrackingService.HasAbility(tracker.RequiredAbility))
                    continue;

                var enabled = charConfig.IsEventEnabled(tracker.EventKey);
                if (ImGui.Checkbox(tracker.DisplayName, ref enabled))
                {
                    charConfig.SetEventEnabled(tracker.EventKey, enabled);
                    configService.Save();
                    eventTrackingService.UpdateTrackerStates();
                }
            }

            ImGui.Unindent();
        }
    }
}
