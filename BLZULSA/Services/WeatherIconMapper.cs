using System.Collections.Frozen;

namespace BLZULSA.Services;
public static class WeatherIconMapper
{
    private const string Fallback = "help";

    private static readonly FrozenDictionary<string, string> IconsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["01d"] = "sunny",
            ["01n"] = "clear_night",
            ["02d"] = "partly_cloudy_day",
            ["02n"] = "partly_cloudy_night",
            ["03d"] = "cloud",
            ["03n"] = "cloud",
            ["04d"] = "cloud",
            ["04n"] = "cloud",
            ["09d"] = "rainy_heavy",
            ["09n"] = "rainy_heavy",
            ["10d"] = "rainy",
            ["10n"] = "rainy",
            ["11d"] = "thunderstorm",
            ["11n"] = "thunderstorm",
            ["13d"] = "weather_snowy",
            ["13n"] = "weather_snowy",
            ["50d"] = "foggy",
            ["50n"] = "foggy",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string ToIconName(string? iconId) => iconId is not null && IconsById.TryGetValue(iconId, out var name) ? name : Fallback;
}
