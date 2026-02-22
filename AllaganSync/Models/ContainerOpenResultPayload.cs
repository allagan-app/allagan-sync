using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class ContainerOpenResultPayload
{
    [JsonPropertyName("container_item_id")]
    public uint ContainerItemId { get; set; }

    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("is_hq")]
    public bool IsHq { get; set; }
}
