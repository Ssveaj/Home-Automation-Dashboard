using System.Collections.Immutable;

namespace BLZULSA.Models;
public sealed record DashboardSnapshot
{
    public static readonly DashboardSnapshot Empty = new();
    public ImmutableDictionary<string, CityWeather> Cities { get; init; } = ImmutableDictionary<string, CityWeather>.Empty;
    public NameDayInfo? NameDay { get; init; }
    public ImmutableArray<FuelPrice> FuelPrices { get; init; } = [];
    public ImmutableArray<string> FuelMunicipalities { get; init; } = [];
    public ElectricityPrices? Electricity { get; init; }
    public ImmutableArray<NewsItem> News { get; init; } = [];
    public ImmutableArray<NewsItem> FinanceNews { get; init; } = [];
    public ImmutableArray<InterestRate> Rates { get; init; } = [];
    public ImmutableArray<StockQuote> Stocks { get; init; } = [];
    public ImmutableArray<CalendarEvent> Events { get; init; } = [];
    public ImmutableArray<StopBoard> Departures { get; init; } = [];
    public ImmutableArray<SchoolMenu> Menus { get; init; } = [];
    public ImmutableDictionary<string, IndoorReading> Indoor { get; init; } = ImmutableDictionary<string, IndoorReading>.Empty;
    public ImmutableDictionary<string, LightState> Lights { get; init; } = ImmutableDictionary<string, LightState>.Empty;
    public ImmutableDictionary<string, DateTimeOffset> LastSuccessUtc { get; init; } = ImmutableDictionary<string, DateTimeOffset>.Empty;
    public bool IsStale(string source, TimeSpan tolerance) => !LastSuccessUtc.TryGetValue(source, out var at) || DateTimeOffset.UtcNow - at > tolerance;
}
public sealed record CityWeather(string Key, string DisplayName, double TemperatureC, double FeelsLikeC, double WindSpeedMs, int HumidityPercent, string? Description, string? IconId);
public sealed record NameDayInfo(DateOnly Date, ImmutableArray<string> Names, string? Holiday);
public sealed record FuelReport(ImmutableArray<FuelPrice> Cheapest, ImmutableArray<string> Municipalities);
public sealed record FuelPrice(string Brand, string? Address, string? Municipality, string FuelType, decimal Price)
{
    public string? Location => (Address, Municipality) switch
    {
        (null, null) => null,
        (null, var m) => m,
        (var a, null) => a,
        var (a, m) => $"{a}, {m}",
    };
}
public sealed record IndoorReading(string Key, string DisplayName, decimal TemperatureC, double HumidityPercent, int BatteryPercent);
