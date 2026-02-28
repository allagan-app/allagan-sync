using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class DutyRewardPayload
{
    [JsonPropertyName("territory_type_id")]
    public ushort TerritoryTypeId { get; set; }

    [JsonPropertyName("map_id")]
    public uint MapId { get; set; }

    [JsonPropertyName("items")]
    public List<DutyRewardItem> Items { get; set; } = new();
}

public class DutyRewardItem
{
    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
