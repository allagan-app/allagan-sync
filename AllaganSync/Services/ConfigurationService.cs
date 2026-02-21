using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class ConfigurationService
{
    private const int CurrentSchemaVersion = 2;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPlayerState playerState;
    private readonly Configuration configuration;

    public ConfigurationService(IDalamudPluginInterface pluginInterface, IPlayerState playerState)
    {
        this.pluginInterface = pluginInterface;
        this.playerState = playerState;
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MigrateIfNeeded();
    }

    public bool IsLoggedIn => playerState.ContentId != 0;

    public CharacterConfig? CurrentCharacter
    {
        get
        {
            var contentId = playerState.ContentId;
            if (contentId == 0)
                return null;

            return configuration.GetCharacterConfig(contentId);
        }
    }

    public bool DebugOverridesEnabled
    {
        get => configuration.DebugOverridesEnabled;
        set => configuration.DebugOverridesEnabled = value;
    }

    public string DebugBaseUrlOverride
    {
        get => configuration.DebugBaseUrlOverride;
        set => configuration.DebugBaseUrlOverride = value;
    }

    public string DebugTokenOverride
    {
        get => configuration.DebugTokenOverride;
        set => configuration.DebugTokenOverride = value;
    }

    public void Save()
    {
        configuration.Save(pluginInterface);
    }

    public void Reset()
    {
        configuration.Characters.Clear();
        configuration.DebugOverridesEnabled = false;
        configuration.DebugBaseUrlOverride = string.Empty;
        configuration.DebugTokenOverride = string.Empty;
        configuration.Version = CurrentSchemaVersion;
        Save();
    }

    private void MigrateIfNeeded()
    {
        if (configuration.Version < 1)
        {
            foreach (var (_, charConfig) in configuration.Characters)
                charConfig.MigrateLegacySyncProperties();
            configuration.Version = 1;
            Save();
        }

        if (configuration.Version < CurrentSchemaVersion)
        {
            foreach (var (_, charConfig) in configuration.Characters)
                charConfig.MigrateLegacyTrackingProperties();
            configuration.Version = CurrentSchemaVersion;
            Save();
        }
    }
}
