using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AllaganSync;

public class CharacterConfig
{
    public string ApiToken { get; set; } = string.Empty;
    public bool SyncMounts { get; set; } = true;
    public bool SyncMinions { get; set; } = true;
    public bool SyncOrchestrions { get; set; } = true;
    public bool SyncEmotes { get; set; } = true;
    public bool SyncTitles { get; set; } = true;
    public bool SyncAchievements { get; set; } = true;
    public bool SyncBardings { get; set; } = true;
    public bool SyncTripleTriadCards { get; set; } = true;
    public bool SyncFashionAccessories { get; set; } = true;
    public bool SyncFacewear { get; set; } = true;
    public bool SyncVistas { get; set; } = true;
    public bool SyncFish { get; set; } = true;
    public bool SyncBlueMageSpells { get; set; } = true;
    public bool SyncCharacterCustomizations { get; set; } = true;

    public bool HasApiToken => !string.IsNullOrEmpty(ApiToken);
}

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public Dictionary<ulong, CharacterConfig> Characters { get; set; } = new();

    public CharacterConfig GetCharacterConfig(ulong characterId)
    {
        if (!Characters.TryGetValue(characterId, out var config))
        {
            config = new CharacterConfig();
            Characters[characterId] = config;
        }
        return config;
    }

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.SavePluginConfig(this);
    }
}
