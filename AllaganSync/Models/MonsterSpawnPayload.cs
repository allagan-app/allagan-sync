using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class MonsterSpawnPayload
{
    [JsonPropertyName("bnpc_base_id")]
    public uint BnpcBaseId { get; set; }

    [JsonPropertyName("territory_type_id")]
    public uint TerritoryTypeId { get; set; }

    [JsonPropertyName("position_x")]
    public float PositionX { get; set; }

    [JsonPropertyName("position_y")]
    public float PositionY { get; set; }

    [JsonPropertyName("position_z")]
    public float PositionZ { get; set; }

    [JsonPropertyName("level")]
    public byte Level { get; set; }
}
