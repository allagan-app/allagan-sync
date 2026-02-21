using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class EventIngestRequest
{
    [JsonPropertyName("events")]
    public List<TrackedEvent> Events { get; set; } = new();
}
