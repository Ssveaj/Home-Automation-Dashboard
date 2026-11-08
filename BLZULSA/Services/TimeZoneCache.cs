using System.Collections.Concurrent;

namespace BLZULSA.Services;

public static class TimeZoneCache
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new(StringComparer.OrdinalIgnoreCase);
    public const string SwedishTimeZoneId = "Europe/Stockholm";
    public static TimeZoneInfo Get(string timeZoneId) => Cache.GetOrAdd(timeZoneId, static id =>
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        });

    public static DateTime Now(string timeZoneId) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Get(timeZoneId));
    private static IReadOnlyList<string>? suggestedIds;
    private static readonly string[] CommonIds =
    [
        "Europe/Stockholm", "Europe/Oslo", "Europe/Copenhagen", "Europe/Helsinki",
        "Europe/London", "Europe/Berlin", "Europe/Paris", "Europe/Madrid",
        "Europe/Rome", "Europe/Amsterdam", "Europe/Lisbon", "Europe/Dublin",
        "Europe/Warsaw", "Europe/Athens", "Europe/Istanbul", "Europe/Moscow",
        "Atlantic/Reykjavik", "Atlantic/Canary",
        "America/Vancouver", "America/Los_Angeles", "America/Denver",
        "America/Chicago", "America/New_York", "America/Toronto",
        "America/Mexico_City", "America/Bogota", "America/Sao_Paulo",
        "America/Argentina/Buenos_Aires",
        "Asia/Kolkata", "Asia/Tokyo", "Asia/Seoul", "Asia/Shanghai",
        "Asia/Hong_Kong", "Asia/Singapore", "Asia/Bangkok", "Asia/Jakarta",
        "Asia/Dubai", "Asia/Jerusalem", "Asia/Karachi", "Asia/Manila",
        "Australia/Sydney", "Australia/Melbourne", "Australia/Brisbane",
        "Australia/Perth", "Pacific/Auckland", "Pacific/Honolulu",
        "Africa/Cairo", "Africa/Lagos", "Africa/Nairobi", "Africa/Johannesburg",
        "UTC",
    ];

    public static IReadOnlyList<string> SuggestedIds => suggestedIds ??= BuildSuggestedIds();
    private static List<string> BuildSuggestedIds()
    {
        var ids = new SortedSet<string>(CommonIds, StringComparer.OrdinalIgnoreCase);

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            ids.Add(TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id);
        }

        return [.. ids];
    }
    public static bool Exists(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
public static class SwedishTime
{
    public static DateTime Now => TimeZoneCache.Now(TimeZoneCache.SwedishTimeZoneId);
}
