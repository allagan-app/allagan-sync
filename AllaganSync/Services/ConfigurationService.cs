using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class ConfigurationService
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPlayerState playerState;
    private readonly Configuration configuration;

    public ConfigurationService(IDalamudPluginInterface pluginInterface, IPlayerState playerState)
    {
        this.pluginInterface = pluginInterface;
        this.playerState = playerState;
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
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

    public void Save()
    {
        configuration.Save(pluginInterface);
    }
}
