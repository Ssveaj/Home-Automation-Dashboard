
using BLZULSA.Models;

namespace BLZULSA.Clients;

public sealed class NameDayClient(HttpClient http, ILogger<NameDayClient> logger)
{
    public async Task<NameDayInfo?> GetNameDayAsync(DateOnly date, CancellationToken ct)
    {
        var url = date.ToString("yyyy'/'MM'/'dd", System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Namnsdag misslyckades: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<NameDayResponse>(ct).ConfigureAwait(false);
            var day = payload?.Dagar.FirstOrDefault();
            if (day is null)
            {
                logger.LogWarning("Namnsdag: svaret innehöll inga dagar.");
                return null;
            }

            return new NameDayInfo(date, [.. day.Namnsdag], day.Helgdag);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Namnsdag kunde inte hämtas.");
            return null;
        }
    }
}
