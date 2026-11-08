using System.Collections.Immutable;
using System.Text.Json.Serialization;
using BLZULSA.Configuration;
using BLZULSA.Models;

namespace BLZULSA.Clients;

public sealed class RiksbankClient(HttpClient http, ILogger<RiksbankClient> logger)
{
    private sealed class Observation
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }

    public async Task<ImmutableArray<InterestRate>> GetRatesAsync(IReadOnlyList<InterestRateSeries> series, CancellationToken ct)
    {
        if (series.Count == 0)
        {
            return [];
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromMinutes(1));

        var results = new List<InterestRate>(series.Count);
        foreach (var item in series)
        {
            if (budget.IsCancellationRequested)
            {
                logger.LogWarning("Hann inte hämta alla räntor innan tiden tog slut — {Count} av {Total} kom med.", results.Count, series.Count);
                break;
            }

            var rate = await GetRateAsync(item, budget.Token).ConfigureAwait(false);
            if (rate is not null)
            {
                results.Add(rate);
            }
        }

        return [.. results];
    }

    private async Task<InterestRate?> GetRateAsync(InterestRateSeries series, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(series.SeriesId))
        {
            return null;
        }

        try
        {
            var response = await http.GetAsync($"Observations/Latest/{Uri.EscapeDataString(series.SeriesId)}", ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                logger.LogWarning("Riksbanken känner inte igen serien {Series}.", series.SeriesId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ränta {Series} misslyckades: HTTP {Status}", series.SeriesId, (int)response.StatusCode);
                return null;
            }

            var observation = await response.Content.ReadFromJsonAsync<Observation>(ct).ConfigureAwait(false);
            if (observation is null || !DateOnly.TryParse(observation.Date, out var observed))
            {
                return null;
            }

            var label = string.IsNullOrWhiteSpace(series.Label) ? series.SeriesId : series.Label;

            return new InterestRate(series.SeriesId, label, observation.Value, observed);
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException
            or Polly.Timeout.TimeoutRejectedException
            or Polly.CircuitBreaker.BrokenCircuitException)
        {
            logger.LogWarning(ex, "Ränta {Series} kunde inte hämtas.", series.SeriesId);
            return null;
        }
    }
}
