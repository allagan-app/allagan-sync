using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class DesynthResultPayload
{
    [JsonPropertyName("source_item_id")]
    public uint SourceItemId { get; set; }

    [JsonPropertyName("desynth_level")]
    public float DesynthLevel { get; set; }

    [JsonPropertyName("results")]
    public List<DesynthResultItem> Results { get; set; } = new();
}

public class DesynthResultItem
{
    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("is_hq")]
    public bool IsHq { get; set; }
}
