using System.Globalization;

namespace BLZULSA.Models;
public sealed record CalendarEvent(string Title, DateTime Start, DateTime End, bool AllDay, string? Location, string? Calendar)
{
    public DateOnly Day => DateOnly.FromDateTime(Start);
    public string When => AllDay ? "Heldag" : End > Start ? $"{Start:HH:mm}–{End:HH:mm}" : Start.ToString("HH:mm", CultureInfo.InvariantCulture);
}
public sealed record Departure(string Line, string Destination, DateTime Scheduled, DateTime Expected, string? Track)
{
    public int MinutesLeft => (int)Math.Round((Expected - DateTime.Now).TotalMinutes);
    public int? DelayMinutes
    {
        get
        {
            var delay = (int)Math.Round((Expected - Scheduled).TotalMinutes);
            return delay > 0 ? delay : null;
        }
    }
    public string Countdown => MinutesLeft switch
    {
        <= 0 => "Nu",
        1 => "1 min",
        var m => $"{m} min",
    };
}
public sealed record StopBoard(string StopId, string Name, IReadOnlyList<Departure> Departures);
public sealed record StopSuggestion(string StopId, string Name, string? Area);
public sealed record SchoolMenuDay(DateOnly Date, IReadOnlyList<string> Meals);
public sealed record SchoolMenu(string Slug, string Name, IReadOnlyList<SchoolMenuDay> Days);
