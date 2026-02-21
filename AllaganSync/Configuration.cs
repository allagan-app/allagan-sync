using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AllaganSync;

public class CharacterConfig
{
    public string ApiToken { get; set; } = string.Empty;

    // Collection sync toggles (dictionary-based, default true for unknown keys)
    public Dictionary<string, bool> SyncCollections { get; set; } = new();

    // Event Tracking
    public bool TrackingEnabled { get; set; } = false;
    public bool TrackingPaused { get; set; } = false;

    // Event tracking toggles (dictionary-based, default true for unknown keys)
    public Dictionary<string, bool> TrackEvents { get; set; } = new();

    public bool HasApiToken => !string.IsNullOrEmpty(ApiToken);

    public bool IsCollectionEnabled(string collectionKey)
    {
        return !SyncCollections.TryGetValue(collectionKey, out var enabled) || enabled;
    }

    public void SetCollectionEnabled(string collectionKey, bool enabled)
    {
        SyncCollections[collectionKey] = enabled;
    }

    public bool IsEventEnabled(string eventKey)
    {
        return !TrackEvents.TryGetValue(eventKey, out var enabled) || enabled;
    }

    public void SetEventEnabled(string eventKey, bool enabled)
    {
        TrackEvents[eventKey] = enabled;
    }

    // Legacy properties for deserialization of v1 configs
    [JsonInclude] public bool? TrackDesynth { internal get; set; }

    // Legacy properties for deserialization of v0 configs
    [JsonInclude] public bool? SyncMounts { internal get; set; }
    [JsonInclude] public bool? SyncMinions { internal get; set; }
    [JsonInclude] public bool? SyncOrchestrions { internal get; set; }
    [JsonInclude] public bool? SyncEmotes { internal get; set; }
    [JsonInclude] public bool? SyncTitles { internal get; set; }
    [JsonInclude] public bool? SyncAchievements { internal get; set; }
    [JsonInclude] public bool? SyncBardings { internal get; set; }
    [JsonInclude] public bool? SyncTripleTriadCards { internal get; set; }
    [JsonInclude] public bool? SyncFashionAccessories { internal get; set; }
    [JsonInclude] public bool? SyncFacewear { internal get; set; }
    [JsonInclude] public bool? SyncVistas { internal get; set; }
    [JsonInclude] public bool? SyncFish { internal get; set; }
    [JsonInclude] public bool? SyncBlueMageSpells { internal get; set; }
    [JsonInclude] public bool? SyncCharacterCustomizations { internal get; set; }

    internal void MigrateLegacySyncProperties()
    {
        MigrateLegacyProperty("mounts", SyncMounts);
        MigrateLegacyProperty("minions", SyncMinions);
        MigrateLegacyProperty("orchestrions", SyncOrchestrions);
        MigrateLegacyProperty("emotes", SyncEmotes);
        MigrateLegacyProperty("titles", SyncTitles);
        MigrateLegacyProperty("achievements", SyncAchievements);
        MigrateLegacyProperty("bardings", SyncBardings);
        MigrateLegacyProperty("triple_triad_cards", SyncTripleTriadCards);
        MigrateLegacyProperty("fashion_accessories", SyncFashionAccessories);
        MigrateLegacyProperty("facewears", SyncFacewear);
        MigrateLegacyProperty("vistas", SyncVistas);
        MigrateLegacyProperty("fish", SyncFish);
        MigrateLegacyProperty("blue_mage_spells", SyncBlueMageSpells);
        MigrateLegacyProperty("character_customizations", SyncCharacterCustomizations);

        // Clear legacy properties
        SyncMounts = null;
        SyncMinions = null;
        SyncOrchestrions = null;
        SyncEmotes = null;
        SyncTitles = null;
        SyncAchievements = null;
        SyncBardings = null;
        SyncTripleTriadCards = null;
        SyncFashionAccessories = null;
        SyncFacewear = null;
        SyncVistas = null;
        SyncFish = null;
        SyncBlueMageSpells = null;
        SyncCharacterCustomizations = null;
    }

    internal void MigrateLegacyTrackingProperties()
    {
        if (TrackDesynth.HasValue)
            TrackEvents["desynth_result"] = TrackDesynth.Value;
        TrackDesynth = null;
    }

    private void MigrateLegacyProperty(string key, bool? legacyValue)
    {
        if (legacyValue.HasValue)
        {
            SyncCollections[key] = legacyValue.Value;
        }
    }
}

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public Dictionary<ulong, CharacterConfig> Characters { get; set; } = new();

    /// <summary>Debug-only: enables override of API base URL and token. Only used in DEBUG builds.</summary>
    public bool DebugOverridesEnabled { get; set; } = false;

    /// <summary>Debug-only: overrides the API base URL when DebugOverridesEnabled is true.</summary>
    public string DebugBaseUrlOverride { get; set; } = string.Empty;

    /// <summary>Debug-only: overrides the API token when DebugOverridesEnabled is true. Do not use in production.</summary>
    [JsonIgnore]
    public string DebugTokenOverride { get; set; } = string.Empty;

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
