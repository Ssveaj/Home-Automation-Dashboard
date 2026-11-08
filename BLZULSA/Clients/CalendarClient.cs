using System.Collections.Immutable;
using BLZULSA.Configuration;
using BLZULSA.Services;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;


using AppEvent = BLZULSA.Models.CalendarEvent;

namespace BLZULSA.Clients;
public sealed class CalendarClient(HttpClient http, ILogger<CalendarClient> logger)
{
    private static readonly TimeZoneInfo Swedish = TimeZoneCache.Get(TimeZoneCache.SwedishTimeZoneId);

    public async Task<ImmutableArray<AppEvent>> GetEventsAsync(CalendarOptions settings, CancellationToken ct)
    {
        var feeds = settings.Feeds.Where(f => !string.IsNullOrWhiteSpace(f.Url)).ToList();

        if (feeds.Count == 0)
        {
            return [];
        }

        var from = SwedishTime.Now.Date;
        var to = from.AddDays(Math.Clamp(settings.DaysAhead, 1, 365));

        var all = new List<AppEvent>();
        var anySucceeded = false;

        foreach (var feed in feeds)
        {
            var events = await ReadFeedAsync(feed, from, to, ct).ConfigureAwait(false);
            if (events is null)
            {
                continue;
            }

            anySucceeded = true;
            all.AddRange(events);
        }

        if (!anySucceeded)
        {
            return [];
        }

        return
        [
            .. all
                .OrderBy(e => e.Start)
                .ThenBy(e => e.Title, StringComparer.CurrentCulture)
                .Take(Math.Clamp(settings.MaxItems, 1, 100)),
        ];
    }

    private async Task<List<AppEvent>?> ReadFeedAsync(CalendarFeedOptions feed, DateTime from, DateTime to, CancellationToken ct)
    {
        try
        {
            var url = feed.Url.Trim();
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url["webcal://".Length..];
            }

            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kalendern {Name} svarade HTTP {Status}.", feed.Name, (int)response.StatusCode); 
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var calendar = Ical.Net.Calendar.Load(text);

            if (calendar is null)
            {
                logger.LogWarning("Kalendern {Name} innehöll ingen giltig ICS-data.", feed.Name);
                return null;
            }

            var events = new List<AppEvent>();
            var until = new CalDateTime(to);

            foreach (var occurrence in calendar.GetOccurrences(new CalDateTime(from)).TakeWhile(o => o.Period.StartTime.CompareTo(until) < 0))
            {
                if (occurrence.Source is not CalendarEvent source)
                {
                    continue;
                }

                var start = occurrence.Period.StartTime;
                var allDay = !start.HasTime;

                var startLocal = ToSwedish(start);
                var endLocal = occurrence.Period.EffectiveEndTime is { } end? ToSwedish(end): startLocal;

                events.Add(new AppEvent(Title: string.IsNullOrWhiteSpace(source.Summary) ? "(utan rubrik)" : source.Summary,Start: startLocal, End: endLocal, AllDay: allDay,
                                                                        Location: string.IsNullOrWhiteSpace(source.Location) ? null : source.Location,
                                                                        Calendar: string.IsNullOrWhiteSpace(feed.Name) ? null : feed.Name));
            }

            return events;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Kalendern {Name} kunde inte hämtas.", feed.Name);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kalendern {Name} kunde inte tolkas.", feed.Name);
            return null;
        }
    }
    private static DateTime ToSwedish(CalDateTime value)
    {
        var raw = value.Value;

        if (raw.Kind == DateTimeKind.Utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(raw, Swedish);
        }

        if (!string.IsNullOrWhiteSpace(value.TzId))
        {
            var source = TimeZoneCache.Get(value.TzId);
            var unspecified = DateTime.SpecifyKind(raw, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(unspecified, source, Swedish);
        }

        return DateTime.SpecifyKind(raw, DateTimeKind.Unspecified);
    }
}
