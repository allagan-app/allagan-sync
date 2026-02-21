using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class RetainerMissionResultPayload
{
    [JsonPropertyName("retainer_task_id")]
    public uint RetainerTaskId { get; set; }

    [JsonPropertyName("retainer_level")]
    public byte RetainerLevel { get; set; }

    [JsonPropertyName("max_level")]
    public bool MaxLevel { get; set; }

    [JsonPropertyName("results")]
    public List<RetainerMissionResultItem> Results { get; set; } = new();
}

public class RetainerMissionResultItem
{
    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("is_hq")]
    public bool IsHq { get; set; }
}
