using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using BLZULSA.Models;

namespace BLZULSA.Clients;

public sealed class GasolinePriceClient(HttpClient http, ILogger<GasolinePriceClient> logger)
{
    private static readonly ImmutableArray<string> InterestingFuels = ["95", "98", "diesel", "etanol"];
    public async Task<FuelReport?> GetPricesAsync(string url, IReadOnlyCollection<string> municipalities, CancellationToken ct)
    {
        try
        {
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Bensinpriser misslyckades: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Parse(json, municipalities);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Bensinpriser kunde inte hämtas.");
            return null;
        }
    }

    internal static FuelReport Parse(string json, IReadOnlyCollection<string>? municipalities = null)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new FuelReport([], []);
        }

        var filter = municipalities is { Count: > 0 } ? municipalities.ToFrozenSet(StringComparer.OrdinalIgnoreCase) : null;
        var cheapestByFuel = new Dictionary<string, FuelPrice>(StringComparer.OrdinalIgnoreCase);
        var seen = new SortedSet<string>(StringComparer.CurrentCulture);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!TryParseEntry(property, out var entry))
            {
                continue;
            }

            if (entry.Municipality is { } place)
            {
                seen.Add(place);
            }

            if (filter is not null && (entry.Municipality is null || !filter.Contains(entry.Municipality)))
            {
                continue;
            }

            if (!cheapestByFuel.TryGetValue(entry.FuelType, out var current) || entry.Price < current.Price)
            {
                cheapestByFuel[entry.FuelType] = entry;
            }
        }

        return new FuelReport([.. InterestingFuels.Where(cheapestByFuel.ContainsKey).Select(fuel => cheapestByFuel[fuel])], [.. seen]);
    }

    private static bool TryParseEntry(JsonProperty property, out FuelPrice entry)
    {
        entry = default!;
        var price = property.Value.ValueKind switch
        {
            JsonValueKind.String => decimal.TryParse(property.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s) ? s : 0,
            JsonValueKind.Number => property.Value.TryGetDecimal(out var n) ? n : 0, _ => 0,
        };

        if (price <= 0)
        {
            return false;
        }

        var segments = property.Name.Split("__", StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim('_')).Where(s => s.Length > 0).ToArray();
        if (segments.Length < 2)
        {
            return false;
        }

        var fuel = segments[^1];
        if (!InterestingFuels.Contains(fuel, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var head = segments[0];
        var underscore = head.IndexOf('_');
        if (underscore >= 0)
        {
            head = head[(underscore + 1)..];
        }

        var (brand, municipality, addressStart) = SplitBrandAndPlace(head, segments);
        if (brand.Length == 0)
        {
            return false;
        }

        var address = segments.Length > addressStart ? string.Join(", ", segments[addressStart..^1].Select(Humanize)) : null;
        entry = new FuelPrice(Humanize(brand), string.IsNullOrWhiteSpace(address) ? null : address, municipality, fuel, price);

        return true;
    }
    private static (string Brand, string? Municipality, int AddressStart) SplitBrandAndPlace(string head, string[] segments)
    {
        var parts = head.Split('_', StringSplitOptions.RemoveEmptyEntries);

        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (i > 0 && SwedishMunicipalities.Resolve($"{parts[i - 1]}_{parts[i]}") is { } twoWord)
            {
                return (string.Join('_', parts[..(i - 1)]), twoWord, 1);
            }

            if (SwedishMunicipalities.Resolve(parts[i]) is { } oneWord)
            {
                return (string.Join('_', parts[..i]), oneWord, 1);
            }
        }

        if (segments.Length > 2 && SwedishMunicipalities.Resolve(segments[1]) is { } ownSegment)
        {
            return (head, ownSegment, 2);
        }

        return (head, null, 1);
    }
    private static string Humanize(string value) => value.Replace('_', ' ').Trim() switch
        {
            "Ovriga" => "Övriga",
            var other => other,
        };
}
