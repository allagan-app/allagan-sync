using System;
using System.Numerics;
using AllaganSync.Services;
using Dalamud.Bindings.ImGui;

namespace AllaganSync.UI;

public static class SetupGuard
{
    /// <summary>
    /// Draws a setup prompt if the character is not logged in or has no API token.
    /// Returns true when ready (logged in + token configured), false otherwise.
    /// </summary>
    /// <param name="configService">The configuration service to check login and token state.</param>
    /// <param name="openSettings">Optional callback to open the settings window.</param>
    /// <returns>True when ready (logged in + token configured), false otherwise.</returns>
    public static bool Draw(ConfigurationService configService, Action? openSettings = null)
    {
        if (!configService.IsLoggedIn)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Please log in to a character first.");
            return false;
        }

        var charConfig = configService.CurrentCharacter;
        if (charConfig is { HasApiToken: true })
            return true;

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "An API token is required.");
        if (openSettings != null)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Open Settings"))
                openSettings();
        }

        return false;
    }
}
