using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BLZULSA.Models;

namespace BLZULSA.Clients;

public sealed partial class NewsClient(HttpClient http, ILogger<NewsClient> logger)
{
    public async Task<ImmutableArray<NewsItem>> GetHeadlinesAsync(string url, int max, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return [];
        }

        try
        {
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nyhetsflödet svarade HTTP {Status}", (int)response.StatusCode);
                return [];
            }

            var xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Parse(xml, max);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            logger.LogWarning(ex, "Nyhetsflödet kunde inte hämtas.");
            return [];
        }
    }

    internal static ImmutableArray<NewsItem> Parse(string xml, int max)
    {
        var document = XDocument.Parse(xml);
        var items = document.Descendants("item").ToList();

        return
        [
            .. items
                .Select(ToItem)
                .Where(item => item is not null)
                .Select(item => item!)
                .OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue)
                .Take(Math.Clamp(max, 1, 20))
        ];
    }

    private static NewsItem? ToItem(XElement element)
    {
        var title = Clean(element.Element("title")?.Value);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new NewsItem(title, Shorten(Clean(element.Element("description")?.Value)), element.Element("link")?.Value, ParseDate(element.Element("pubDate")?.Value));
    }
    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)? parsed : null;
    private static string Shorten(string value)
    {
        const int limit = 220;

        if (value.Length <= limit)
        {
            return value;
        }

        var cut = value.LastIndexOf(' ', limit);
        return string.Concat(value.AsSpan(0, cut > 80 ? cut : limit).TrimEnd(), "…");
    }
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(TagPattern().Replace(value, " "));
        return WhitespacePattern().Replace(text, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
