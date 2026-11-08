namespace BLZULSA.Services;

public static class IntervalLimits
{
    private static readonly Dictionary<string, TimeSpan> Minimums = new(StringComparer.Ordinal)
    {
        [DashboardRefreshService.WeatherSource] = TimeSpan.FromHours(1),
        [DashboardRefreshService.NameDaySource] = TimeSpan.FromHours(1),
        [DashboardRefreshService.NewsSource] = TimeSpan.FromMinutes(15),
        [DashboardRefreshService.FinanceNewsSource] = TimeSpan.FromMinutes(15),
        [DashboardRefreshService.ElectricitySource] = TimeSpan.FromMinutes(60),
        [DashboardRefreshService.FuelSource] = TimeSpan.FromMinutes(60),
        [DashboardRefreshService.RatesSource] = TimeSpan.FromHours(6),
        [DashboardRefreshService.StocksSource] = TimeSpan.FromMinutes(15),
        [DashboardRefreshService.CalendarSource] = TimeSpan.FromMinutes(15),
        [DashboardRefreshService.DeparturesSource] = TimeSpan.FromMinutes(2),
        [DashboardRefreshService.SchoolMenuSource] = TimeSpan.FromHours(6),
        [DashboardRefreshService.LightsSource] = TimeSpan.FromSeconds(15),
        [DashboardRefreshService.IndoorSource] = TimeSpan.FromMinutes(5),
    };

    public static TimeSpan Minimum(string source) => Minimums.TryGetValue(source, out var min) ? min : TimeSpan.FromMinutes(1);

    public static TimeSpan Clamp(string source, TimeSpan value) => value < Minimum(source) ? Minimum(source) : value;
}
