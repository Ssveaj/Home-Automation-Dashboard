using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Clients;

public sealed class ResRobotClient(HttpClient http, IOptionsMonitor<DashboardOptions> options, ILogger<ResRobotClient> logger)
{
    private TransitOptions Settings => options.CurrentValue.Transit;
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Settings.ApiKey);
    private sealed class DepartureBoard
    {
        [JsonPropertyName("Departure")]
        public List<DepartureDto>? Departure { get; set; }
    }

    private sealed class DepartureDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("rtDate")]
        public string? RealTimeDate { get; set; }

        [JsonPropertyName("rtTime")]
        public string? RealTimeTime { get; set; }

        [JsonPropertyName("rtTrack")]
        public string? RealTimeTrack { get; set; }

        [JsonPropertyName("track")]
        public string? Track { get; set; }

        [JsonPropertyName("Product")]
        public List<ProductDto>? Product { get; set; }
    }

    private sealed class ProductDto
    {
        [JsonPropertyName("line")]
        public string? Line { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class LocationResponse
    {
        [JsonPropertyName("stopLocationOrCoordLocation")]
        public List<LocationWrapper>? Locations { get; set; }
    }

    private sealed class LocationWrapper
    {
        [JsonPropertyName("StopLocation")]
        public StopLocationDto? StopLocation { get; set; }
    }

    private sealed class StopLocationDto
    {
        [JsonPropertyName("extId")]
        public string? ExtId { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public async Task<ImmutableArray<StopBoard>> GetBoardsAsync(IReadOnlyList<TransitStopOptions> stops, CancellationToken ct)
    {
        if (stops.Count == 0 || !IsEnabled)
        {
            return [];
        }

        var boards = new List<StopBoard>(stops.Count);
        foreach (var stop in stops)
        {
            if (string.IsNullOrWhiteSpace(stop.StopId))
            {
                continue;
            }

            var departures = await GetBoardAsync(stop.StopId, ct).ConfigureAwait(false);

            if (departures is null)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(stop.Name) ? stop.StopId : stop.Name;
            boards.Add(new StopBoard(stop.StopId, name, departures));
        }

        return [.. boards];
    }

    private async Task<IReadOnlyList<Departure>?> GetBoardAsync(string stopId, CancellationToken ct)
    {
        try
        {
            var duration = Math.Clamp(Settings.WindowMinutes, 5, 1439);
            var url = $"departureBoard?id={Uri.EscapeDataString(stopId.Trim())}" + $"&duration={duration}&format=json&accessId={Settings.ApiKey}";
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Avgångar för {Stop} misslyckades: {Reason}", stopId, Explain(response.StatusCode));
                return null;
            }

            var board = await response.Content.ReadFromJsonAsync<DepartureBoard>(ct).ConfigureAwait(false);
            if (board?.Departure is null)
            {
                return [];
            }

            var max = Math.Clamp(Settings.MaxDepartures, 1, 30);
            return
            [
                .. board.Departure.Select(ToDeparture).Where(d => d is not null).Select(d => d!).Where(d => d.MinutesLeft >= -1).OrderBy(d => d.Expected).Take(max),
            ];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Avgångar för {Stop} kunde inte hämtas.", stopId);
            return null;
        }
    }

    private static Departure? ToDeparture(DepartureDto dto)
    {
        if (!TryParse(dto.Date, dto.Time, out var scheduled))
        {
            return null;
        }

        var expected = TryParse(dto.RealTimeDate ?? dto.Date, dto.RealTimeTime ?? dto.Time, out var live) ? live: scheduled;
        var product = dto.Product?.FirstOrDefault();
        var line = product?.Line?? product?.Name ?? dto.Name ?? "?";

        return new Departure(Line: line.Trim(), Destination: (dto.Direction ?? "okänd").Trim(), Scheduled: scheduled, Expected: expected, Track: string.IsNullOrWhiteSpace(dto.RealTimeTrack ?? dto.Track) 
                                                           ? null : (dto.RealTimeTrack ?? dto.Track)!.Trim());
    }
    private static bool TryParse(string? date, string? time, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time))
        {
            return false;
        }

        string[] formats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm"];
        return DateTime.TryParseExact($"{date.Trim()} {time.Trim()}", formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
    public async Task<IReadOnlyList<StopSuggestion>> SearchStopsAsync(string query, string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            var url = $"location.name?input={Uri.EscapeDataString(query.Trim())}" + $"&format=json&accessId={apiKey}";
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Hållplatssökning misslyckades: {Reason}", Explain(response.StatusCode));
                return [];
            }

            var found = await response.Content.ReadFromJsonAsync<LocationResponse>(ct).ConfigureAwait(false);
            if (found?.Locations is null)
            {
                return [];
            }

            return
            [
                .. found.Locations
                    .Select(w => w.StopLocation)
                    .Where(l => l is not null && !string.IsNullOrWhiteSpace(l.Name))
                    .Select(l => new StopSuggestion(
                        StopId: l!.ExtId ?? l.Id ?? string.Empty,
                        Name: Shorten(l.Name!),
                        Area: AreaOf(l.Name!)))
                    .Where(s => !string.IsNullOrWhiteSpace(s.StopId))
                    .Take(15),
            ];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Hållplatssökning för {Query} kunde inte utföras.", query);
            return [];
        }
    }
    private static string Shorten(string name)
    {
        var paren = name.IndexOf('(', StringComparison.Ordinal);
        return paren > 0 ? name[..paren].Trim() : name.Trim();
    }
    private static string? AreaOf(string name)
    {
        var open = name.IndexOf('(', StringComparison.Ordinal);
        var close = name.LastIndexOf(')');

        return open > 0 && close > open ? name[(open + 1)..close].Trim() : null;
    }
    internal static string Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "ResRobot avvisade anropet — oftast en saknad eller felaktig nyckel. Kontrollera den under Inställningar.",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Nyckeln avvisades av ResRobot.",
        HttpStatusCode.TooManyRequests => "ResRobot strypte anropen — höj intervallet under Allmänt.",
        _ => $"HTTP {(int)status}",
    };
}
