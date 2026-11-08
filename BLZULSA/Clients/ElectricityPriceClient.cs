using System.Collections.Immutable;
using System.Globalization;
using BLZULSA.Models;
using BLZULSA.Services;

namespace BLZULSA.Clients;
public sealed class ElectricityPriceClient(HttpClient http, ILogger<ElectricityPriceClient> logger)
{
    public async Task<ElectricityPrices?> GetPricesAsync(string zone, DateOnly date, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"api/v1/prices/{date:yyyy}/{date:MM-dd}_{zone}.json");

        try
        {
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Elpris för {Date} ({Zone}) misslyckades: HTTP {Status}", date, zone, (int)response.StatusCode);
                return null;
            }

            var entries = await response.Content.ReadFromJsonAsync<List<ElectricityPriceEntry>>(ct).ConfigureAwait(false);
            if (entries is null || entries.Count == 0)
            {
                logger.LogWarning("Elpris för {Date} ({Zone}): tomt svar.", date, zone);
                return null;
            }

            return new ElectricityPrices(zone, date, GroupIntoHours(entries));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Elpris för {Date} ({Zone}) kunde inte hämtas.", date, zone);
            return null;
        }
    }
    internal static ImmutableArray<ElectricityHour> GroupIntoHours(List<ElectricityPriceEntry> entries)
    {
        var swedish = TimeZoneCache.Get(TimeZoneCache.SwedishTimeZoneId);

        return
        [
            .. entries
                .GroupBy(e => new DateTimeOffset(
                    TimeZoneInfo.ConvertTime(e.Start, swedish).DateTime.Date.AddHours(
                        TimeZoneInfo.ConvertTime(e.Start, swedish).Hour),
                    TimeZoneInfo.ConvertTime(e.Start, swedish).Offset))
                .OrderBy(g => g.Key)
                .Select(g => new ElectricityHour(
                    g.Key,
                    g.Key.AddHours(1),
                    Math.Round(g.Average(e => e.SekPerKwh), 4)))
        ];
    }
}
