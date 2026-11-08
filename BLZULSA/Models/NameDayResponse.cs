using System.Text.Json.Serialization;

namespace BLZULSA.Models;
public sealed class NameDayResponse
{
    [JsonPropertyName("dagar")]
    public List<NameDayEntry> Dagar { get; set; } = [];
}
public sealed class NameDayEntry
{
    [JsonPropertyName("datum")]
    public string? Datum { get; set; }

    [JsonPropertyName("veckodag")]
    public string? Veckodag { get; set; }

    [JsonPropertyName("namnsdag")]
    public List<string> Namnsdag { get; set; } = [];

    [JsonPropertyName("helgdag")]
    public string? Helgdag { get; set; }

    [JsonPropertyName("flaggdag")]
    public string? Flaggdag { get; set; }
}
