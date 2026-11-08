using System.Globalization;
using System.Text.Json.Serialization;
using BLZULSA.Models;

namespace BLZULSA.Services;

public static class SchoolMenuApi
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    public static IEndpointRouteBuilder MapSchoolMenuApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/school-menu", (DashboardState state) =>
        {
            var snapshot = state.Current;

            return Results.Ok(snapshot.Menus.Select(m => Shape(m, Updated(snapshot))).ToArray());
        });

        app.MapGet("/api/school-menu/{slug}", (string slug, DashboardState state) =>
        {
            var snapshot = state.Current;
            var menu = snapshot.Menus.FirstOrDefault(m => string.Equals(m.Slug, slug, StringComparison.OrdinalIgnoreCase));

            return menu is null
                ? Results.NotFound(new { error = "Ingen matsedel för " + slug + " finns hämtad." })
                : Results.Ok(Shape(menu, Updated(snapshot)));
        });

        return app;
    }

    private static DateTimeOffset? Updated(DashboardSnapshot snapshot) =>
        snapshot.LastSuccessUtc.TryGetValue(DashboardRefreshService.SchoolMenuSource, out var when) ? when : null;

    private static SchoolMenuPayload Shape(SchoolMenu menu, DateTimeOffset? updated) =>
        new(menu.Slug, menu.Name, updated, [.. menu.Days.Select(d => new SchoolMenuDayPayload(d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DayName(d.Date), [.. d.Meals]))]);

    internal static string DayName(DateOnly day)
    {
        var name = Swedish.DateTimeFormat.GetDayName(day.DayOfWeek);

        return char.ToUpper(name[0], Swedish) + name[1..];
    }

    private sealed record SchoolMenuPayload(
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("schoolName")] string SchoolName,
        [property: JsonPropertyName("lastUpdated")] DateTimeOffset? LastUpdated,
        [property: JsonPropertyName("days")] IReadOnlyList<SchoolMenuDayPayload> Days);

    private sealed record SchoolMenuDayPayload(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("dayName")] string DayName,
        [property: JsonPropertyName("meals")] IReadOnlyList<string> Meals);
}
