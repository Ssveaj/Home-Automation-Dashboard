using System.Collections.Immutable;
using System.Net;
using System.Text.Json.Serialization;
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Clients;

public sealed class FinnhubClient(HttpClient http, IOptionsMonitor<DashboardOptions> options, ILogger<FinnhubClient> logger)
{
    private StocksOptions Settings => options.CurrentValue.Stocks;
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Settings.ApiKey);
    private sealed class QuoteResponse
    {
        [JsonPropertyName("c")]
        public decimal Current { get; set; }
        [JsonPropertyName("dp")]
        public decimal? PercentChange { get; set; }
    }
    private sealed class SearchResponse
    {
        [JsonPropertyName("result")]
        public List<SearchHit>? Result { get; set; }
    }
    private sealed class SearchHit
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
    public async Task<ImmutableArray<StockQuote>> GetQuotesAsync(IReadOnlyList<StockOptions> symbols, CancellationToken ct)
    {
        if (symbols.Count == 0 || !IsEnabled)
        {
            return [];
        }

        var results = new List<StockQuote>(symbols.Count);
        foreach (var stock in symbols)
        {
            if (string.IsNullOrWhiteSpace(stock.Symbol))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(stock.Label) ? stock.Symbol : stock.Label;
            var quote = await GetQuoteAsync(stock.Symbol, ct).ConfigureAwait(false);

            results.Add(new StockQuote(stock.Symbol, label, quote?.Price, quote?.Percent));
        }

        return [.. results];
    }

    private async Task<(decimal Price, decimal? Percent)?> GetQuoteAsync(string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"quote?symbol={Uri.EscapeDataString(symbol.Trim())}&token={Settings.ApiKey}";
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kurs för {Symbol} misslyckades: {Reason}", symbol, Explain(response.StatusCode));
                return null;
            }

            var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>(ct).ConfigureAwait(false);
            if (quote is null || quote.Current <= 0)
            {
                logger.LogWarning("Finnhub lämnade ingen kurs för {Symbol}. Gratisnivån täcker i regel bara amerikanska börser.", symbol);
                return null;
            }

            return (quote.Current, quote.PercentChange);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Kurs för {Symbol} kunde inte hämtas.", symbol);
            return null;
        }
    }
    public async Task<IReadOnlyList<StockSuggestion>> SearchAsync(string query, string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            var url = $"search?q={Uri.EscapeDataString(query.Trim())}&token={apiKey}";
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Bolagssökning misslyckades: {Reason}", Explain(response.StatusCode));
                return [];
            }

            var hits = await response.Content.ReadFromJsonAsync<SearchResponse>(ct).ConfigureAwait(false);

            if (hits?.Result is null)
            {
                return [];
            }

            return
            [
                .. hits.Result
                    .Where(h => !string.IsNullOrWhiteSpace(h.Symbol))
                    .Where(h => string.IsNullOrWhiteSpace(h.Type)
                        || h.Type.Contains("Common Stock", StringComparison.OrdinalIgnoreCase)
                        || h.Type.Equals("ETP", StringComparison.OrdinalIgnoreCase))
                    .Take(15)
                    .Select(h => new StockSuggestion(h.Symbol!, h.Description ?? string.Empty, h.Type)),
            ];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Bolagssökning för {Query} kunde inte utföras.", query);
            return [];
        }
    }
    internal static string Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Finnhub avvisade nyckeln — kontrollera den under Inställningar.",
        HttpStatusCode.Forbidden => "Nyckeln saknar behörighet till den här marknaden (kräver betald nivå).",
        HttpStatusCode.TooManyRequests => "Finnhub strypte anropen — höj intervallet under Allmänt.",
        _ => $"HTTP {(int)status}",
    };
}
