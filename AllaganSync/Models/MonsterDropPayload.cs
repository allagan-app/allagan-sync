using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class MonsterDropPayload
{
    [JsonPropertyName("territory_type_id")]
    public ushort TerritoryTypeId { get; set; }

    [JsonPropertyName("map_id")]
    public uint MapId { get; set; }

    [JsonPropertyName("deaths")]
    public List<MonsterDropDeath> Deaths { get; set; } = new();

    [JsonPropertyName("items")]
    public List<MonsterDropItem> Items { get; set; } = new();
}

public class MonsterDropDeath
{
    [JsonPropertyName("bnpc_base_id")]
    public uint BnpcBaseId { get; set; }

    [JsonPropertyName("offset_ms")]
    public long OffsetMs { get; set; }
}

public class MonsterDropItem
{
    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("offset_ms")]
    public long OffsetMs { get; set; }
}
