using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class ChestLootPayload
{
    [JsonPropertyName("territory_type_id")]
    public ushort TerritoryTypeId { get; set; }

    [JsonPropertyName("map_id")]
    public uint MapId { get; set; }

    [JsonPropertyName("chest_base_id")]
    public uint ChestBaseId { get; set; }

    [JsonPropertyName("coffer_kind")]
    public byte CofferKind { get; set; }

    [JsonPropertyName("position_x")]
    public float PositionX { get; set; }

    [JsonPropertyName("position_y")]
    public float PositionY { get; set; }

    [JsonPropertyName("position_z")]
    public float PositionZ { get; set; }

    [JsonPropertyName("items")]
    public List<ChestLootItem> Items { get; set; } = new();
}

public class ChestLootItem
{
    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
