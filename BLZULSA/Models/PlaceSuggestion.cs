using System.Text.Json.Serialization;

namespace BLZULSA.Models;

public sealed class GeocodeResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("local_names")]
    public Dictionary<string, string>? LocalNames { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}
public sealed record PlaceSuggestion(string Name, string? CountryName, string? State, double Lat, double Lon, string TimeZoneId)
{
    public string Query => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"lat={Lat}&lon={Lon}");
    public string Label => string.IsNullOrWhiteSpace(CountryName) ? Name : $"{Name} · {CountryName}";
    public string? Detail => string.IsNullOrWhiteSpace(State) ? null : State;
}
