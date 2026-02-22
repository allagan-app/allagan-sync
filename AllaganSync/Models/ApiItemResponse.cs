using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class ApiItemResponse
{
    [JsonPropertyName("data")]
    public List<ApiItem> Data { get; set; } = new();
}

public class ApiItem
{
    [JsonPropertyName("xiv_id")]
    public uint XivId { get; set; }
}
