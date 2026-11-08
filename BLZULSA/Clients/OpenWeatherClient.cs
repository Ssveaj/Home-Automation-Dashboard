
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Clients;

public sealed class OpenWeatherClient(HttpClient http, IOptionsMonitor<DashboardOptions> options, ILogger<OpenWeatherClient> logger)
{
    private OpenWeatherOptions Settings => options.CurrentValue.OpenWeather;
    public async Task<CityWeather?> GetWeatherAsync(CityOptions city, CancellationToken ct)
    {
        var url = $"weather?{city.Query}&units=metric&lang=sv&appid={Settings.ApiKey}";

        try
        {
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Väder för {City} misslyckades: HTTP {Status}", city.DisplayName, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenWeatherResponse>(ct).ConfigureAwait(false);
            if (payload?.Main is null)
            {
                logger.LogWarning("Väder för {City}: svaret saknade fältet 'main'.", city.DisplayName);
                return null;
            }

            var condition = payload.Weather.FirstOrDefault();

            return new CityWeather(city.Key, city.DisplayName, Math.Round(payload.Main.Temp, 1), Math.Round(payload.Main.FeelsLike, 1), Math.Round(payload.Wind?.Speed ?? 0, 1), payload.Main.Humidity, condition?.Description, condition?.Icon);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Väder för {City} kunde inte hämtas.", city.DisplayName);
            return null;
        }
    }
    public async Task<IReadOnlyList<PlaceSuggestion>> SearchPlacesAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(Settings.ApiKey))
        {
            return [];
        }

        var url = $"/geo/1.0/direct?q={Uri.EscapeDataString(query.Trim())}&limit=8&appid={Settings.ApiKey}";
        try
        {
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Platssökning misslyckades: HTTP {Status}", (int)response.StatusCode);
                return [];
            }

            var results = await response.Content.ReadFromJsonAsync<List<GeocodeResult>>(ct).ConfigureAwait(false);

            return results is null ? [] : [.. results.Select(ToSuggestion)];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Platssökning för {Query} kunde inte utföras.", query);
            return [];
        }
    }

    private static PlaceSuggestion ToSuggestion(GeocodeResult result) => new(result.LocalNames?.GetValueOrDefault("sv") ?? result.Name, CountryName(result.Country), result.State, result.Lat, result.Lon, GeoTimeZone.TimeZoneLookup.GetTimeZone(result.Lat, result.Lon).Result);
    private static string? CountryName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            return new System.Globalization.RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }
}
