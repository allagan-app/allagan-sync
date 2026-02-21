using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class TrackedEvent
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("occurred_at")]
    public string? OccurredAt { get; set; }
}
