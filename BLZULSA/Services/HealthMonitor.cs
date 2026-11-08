using BLZULSA.Configuration;
using BLZULSA.Models;

namespace BLZULSA.Services;

public enum HealthState
{
    Off,
    NeedsSetup,
    Waiting,
    Ok,
    Stale,
}

public sealed record SourceHealth(string Key, string Name, HealthState State, DateTimeOffset? LastSuccess, TimeSpan Interval, string Detail)
{
    public TimeSpan? Age => LastSuccess is { } at ? DateTimeOffset.UtcNow - at : null;
}
public static class HealthMonitor
{
    private const int MissedIntervalsBeforeStale = 3;

    private sealed record Definition(string Key, string Name, Func<DashboardOptions, bool> Enabled,Func<DashboardOptions, string?> Missing, Func<IntervalOptions, TimeSpan> Interval);
    private static readonly Definition[] Definitions =
    [
        new(DashboardRefreshService.WeatherSource, "Väder", o => o.OpenWeather.Cities.Count > 0, o => o.OpenWeather.Cities.Count == 0 ? "ingen ort tillagd" : string.IsNullOrWhiteSpace(o.OpenWeather.ApiKey) ? "OpenWeather-nyckel saknas" : null, i => i.Weather),
        new(DashboardRefreshService.NameDaySource, "Namnsdag", o => o.NameDay.Enabled, _ => null, i => i.NameDay),
        new(DashboardRefreshService.NewsSource, "Nyheter", o => o.News.Enabled, o => string.IsNullOrWhiteSpace(o.News.Url) ? "ingen flödesadress" : null, i => i.News),
        new(DashboardRefreshService.ElectricitySource, "Elpris", o => o.ElectricityPrice.Enabled, _ => null, i => i.Electricity),
        new(DashboardRefreshService.FuelSource, "Drivmedel", o => o.GasolinePrice.Enabled, o => string.IsNullOrWhiteSpace(o.GasolinePrice.Url) ? "ingen adress" : null, i => i.Fuel),
        new(DashboardRefreshService.FinanceNewsSource, "Ekonominyheter", o => o.FinanceNews.Enabled, o => string.IsNullOrWhiteSpace(o.FinanceNews.Url) ? "ingen flödesadress" : null, i => i.FinanceNews),
        new(DashboardRefreshService.RatesSource, "Räntor", o => o.InterestRates.Enabled, o => o.InterestRates.Series.Count == 0 ? "inga serier tillagda" : null, i => i.InterestRates),
        new(DashboardRefreshService.StocksSource, "Aktier", o => o.Stocks.Enabled, o => string.IsNullOrWhiteSpace(o.Stocks.ApiKey) ? "Finnhub-nyckel saknas": o.Stocks.Symbols.Count == 0 ? "inga bolag valda" : null, i => i.Stocks),
        new(DashboardRefreshService.CalendarSource, "Kalender", o => o.Calendar.Enabled, o => o.Calendar.Feeds.Count == 0 ? "ingen ICS-adress" : null, i => i.Calendar),
        new(DashboardRefreshService.DeparturesSource, "Bussavgångar", o => o.Transit.Enabled, o => string.IsNullOrWhiteSpace(o.Transit.ApiKey) ? "ResRobot-nyckel saknas": o.Transit.Stops.Count == 0 ? "ingen hållplats vald" : null, i => i.Departures),
        new(DashboardRefreshService.SchoolMenuSource, "Skollunch", o => o.SchoolMenu.Enabled, o => o.SchoolMenu.Schools.Count == 0 ? "ingen skola vald" : null, i => i.SchoolMenu),
        new(DashboardRefreshService.IndoorSource, "Inomhusgivare", o => o.Shelly.Enabled, o => o.Shelly.Sensors.Count == 0 ? "inga givare tillagda" : o.Shelly.Sensors.All(x => string.IsNullOrWhiteSpace(x.DeviceId)) ? "inga enhets-ID ifyllda" : null, i => i.IndoorSensors),
        new(DashboardRefreshService.LightsSource, "Enheter", o => o.Shelly.Enabled, o => o.Shelly.Lights.Count == 0 ? "inga enheter tillagda" : o.Shelly.Lights.All(l => string.IsNullOrWhiteSpace(l.DeviceId)) ? "inga enhets-ID ifyllda" : null, i => i.Lights),
    ];

    public static IReadOnlyList<SourceHealth> Describe(DashboardOptions options, DashboardSnapshot snapshot)
    {
        var report = new List<SourceHealth>(Definitions.Length);

        foreach (var d in Definitions)
        {
            var interval = IntervalLimits.Clamp(d.Key, d.Interval(options.Intervals));
            snapshot.LastSuccessUtc.TryGetValue(d.Key, out var last);
            DateTimeOffset? lastSuccess = last == default ? null : last;

            var state = Evaluate(d, options, lastSuccess, interval, out var detail);

            report.Add(new SourceHealth(d.Key, d.Name, state, lastSuccess, interval, detail));
        }

        return report;
    }

    private static HealthState Evaluate(Definition d, DashboardOptions options, DateTimeOffset? lastSuccess, TimeSpan interval, out string detail)
    {
        if (!d.Enabled(options))
        {
            detail = "Avstängd under Inställningar.";
            return HealthState.Off;
        }

        if (d.Missing(options) is { } missing)
        {
            detail = char.ToUpper(missing[0], System.Globalization.CultureInfo.CurrentCulture) + missing[1..] + ".";
            return HealthState.NeedsSetup;
        }

        if (lastSuccess is null)
        {
            detail = "Ingen lyckad hämtning ännu.";
            return HealthState.Waiting;
        }

        var age = DateTimeOffset.UtcNow - lastSuccess.Value;

        if (age > interval * MissedIntervalsBeforeStale)
        {
            detail = $"Inget nytt på {Humanize(age)}. Kontrollera nyckel och nätverk.";
            return HealthState.Stale;
        }

        detail = $"Uppdaterad för {Humanize(age)} sedan.";
        return HealthState.Ok;
    }

    public static string Humanize(TimeSpan value) => value switch
    {
        { TotalSeconds: < 90 } => $"{value.TotalSeconds:F0} sekunder",
        { TotalMinutes: < 90 } => $"{value.TotalMinutes:F0} minuter",
        { TotalHours: < 36 } => $"{value.TotalHours:F0} timmar",
        _ => $"{value.TotalDays:F0} dygn",
    };
}
