using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AllaganSync.Models;

public class TripleTriadDuelPayload
{
    [JsonPropertyName("npc_xiv_id")]
    public uint NpcXivId { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("cards_won")]
    public List<TripleTriadCardWon> CardsWon { get; set; } = new();

    [JsonPropertyName("mgp_reward")]
    public int? MgpReward { get; set; }
}

public class TripleTriadCardWon
{
    [JsonPropertyName("item_id")]
    public uint ItemId { get; set; }
}
