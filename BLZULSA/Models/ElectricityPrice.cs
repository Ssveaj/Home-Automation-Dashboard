using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace BLZULSA.Models;

public sealed class ElectricityPriceEntry
{
    [JsonPropertyName("SEK_per_kWh")]
    public decimal SekPerKwh { get; set; }

    [JsonPropertyName("time_start")]
    public DateTimeOffset Start { get; set; }

    [JsonPropertyName("time_end")]
    public DateTimeOffset End { get; set; }
}

public sealed record ElectricityPrices(string Zone, DateOnly Date, ImmutableArray<ElectricityHour> Hours)
{
    public ElectricityHour? Current => Hours.FirstOrDefault(h => h.Start <= DateTimeOffset.Now && DateTimeOffset.Now < h.End);
    public decimal Lowest => Hours.IsEmpty ? 0 : Hours.Min(h => h.SekPerKwh);
    public decimal Highest => Hours.IsEmpty ? 0 : Hours.Max(h => h.SekPerKwh);
    public decimal Average => Hours.IsEmpty ? 0 : Hours.Average(h => h.SekPerKwh);
    public ElectricityHour? Cheapest => Hours.IsEmpty ? null : Hours.MinBy(h => h.SekPerKwh);
}
public sealed record ElectricityHour(DateTimeOffset Start, DateTimeOffset End, decimal SekPerKwh);
