using System.Collections.Immutable;
using BLZULSA.Clients;
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Services;

public sealed class DashboardRefreshService(IServiceScopeFactory scopeFactory, DashboardState state, RefreshTrigger trigger, IOptionsMonitor<DashboardOptions> options, ILogger<DashboardRefreshService> logger) : BackgroundService
{
    public const string WeatherSource = "weather";
    public const string NameDaySource = "nameday";
    public const string FuelSource = "fuel";
    public const string ElectricitySource = "electricity";
    public const string NewsSource = "news";
    public const string FinanceNewsSource = "finance-news";
    public const string RatesSource = "rates";
    public const string StocksSource = "stocks";
    public const string CalendarSource = "calendar";
    public const string DeparturesSource = "departures";
    public const string SchoolMenuSource = "school-menu";
    public const string IndoorSource = "indoor";
    public const string LightsSource = "lights";
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(15);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> lastAttempt = new(StringComparer.Ordinal);
    private DashboardOptions? lastSeenSettings;
    private DashboardOptions settings => options.CurrentValue;
    private readonly record struct Fetched<T>(T Value, bool Succeeded);
    private readonly record struct FuelState(ImmutableArray<FuelPrice> Prices, ImmutableArray<string> Municipalities);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var onSettingsChanged = options.OnChange(_ => trigger.Trigger());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Oväntat fel vid uppdatering av dashboarden.");
            }

            if (!await WaitForNextRoundAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }
    private async Task<bool> WaitForNextRoundAsync(CancellationToken ct)
    {
        try
        {
            await Task.WhenAny(Task.Delay(Tick, ct), trigger.Waiter).ConfigureAwait(false);
            return !ct.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        ResetScheduleIfSettingsChanged();

        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var every = settings.Intervals;

        using var rundan = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var due = new List<(string Kalla, Task Uppgift)>(8);

        if (Due(WeatherSource, every.Weather))
        {
            due.Add((WeatherSource, PublishAsync(RefreshWeatherAsync, (s, v) => s with { Cities = v }, WeatherSource, provider, rundan.Token)));
        }

        if (Due(NameDaySource, every.NameDay))
        {
            due.Add((NameDaySource, PublishAsync(RefreshNameDayAsync, (s, v) => s with { NameDay = v }, NameDaySource, provider, rundan.Token)));
        }

        if (Due(FuelSource, every.Fuel))
        {
            due.Add((FuelSource, PublishAsync(RefreshFuelAsync, (s, v) => s with { FuelPrices = v.Prices, FuelMunicipalities = v.Municipalities }, FuelSource, provider, rundan.Token)));
        }

        if (Due(ElectricitySource, every.Electricity))
        {
            due.Add((ElectricitySource, PublishAsync(RefreshElectricityAsync, (s, v) => s with { Electricity = v }, ElectricitySource, provider, rundan.Token)));
        }

        if (Due(NewsSource, every.News))
        {
            due.Add((NewsSource, PublishAsync(RefreshNewsAsync, (s, v) => s with { News = v }, NewsSource, provider, rundan.Token)));
        }

        if (Due(FinanceNewsSource, every.FinanceNews))
        {
            due.Add((FinanceNewsSource, PublishAsync(RefreshFinanceNewsAsync, (s, v) => s with { FinanceNews = v }, FinanceNewsSource, provider, rundan.Token)));
        }

        if (Due(RatesSource, every.InterestRates))
        {
            due.Add((RatesSource, PublishAsync(RefreshRatesAsync, (s, v) => s with { Rates = v }, RatesSource, provider, rundan.Token)));
        }

        if (Due(StocksSource, every.Stocks))
        {
            due.Add((StocksSource, PublishAsync(RefreshStocksAsync, (s, v) => s with { Stocks = v }, StocksSource, provider, rundan.Token)));
        }

        if (Due(CalendarSource, every.Calendar))
        {
            due.Add((CalendarSource, PublishAsync(RefreshCalendarAsync, (s, v) => s with { Events = v }, CalendarSource, provider, rundan.Token)));
        }

        if (Due(DeparturesSource, every.Departures))
        {
            due.Add((DeparturesSource, PublishAsync(RefreshDeparturesAsync, (s, v) => s with { Departures = v }, DeparturesSource, provider, rundan.Token)));
        }

        if (Due(SchoolMenuSource, every.SchoolMenu))
        {
            due.Add((SchoolMenuSource, PublishAsync(RefreshSchoolMenuAsync, (s, v) => s with { Menus = v }, SchoolMenuSource, provider, rundan.Token)));
        }

        if (Due(IndoorSource, every.IndoorSensors))
        {
            due.Add((IndoorSource, PublishAsync(RefreshIndoorAsync, (s, v) => s with { Indoor = v }, IndoorSource, provider, rundan.Token)));
        }

        await InvantaAsync(due, rundan, ct).ConfigureAwait(false);
    }

    private static readonly TimeSpan Rundtak = TimeSpan.FromMinutes(2);

    private async Task InvantaAsync(List<(string Kalla, Task Uppgift)> due, CancellationTokenSource rundan, CancellationToken ct)
    {
        if (due.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(due.Select(d => d.Uppgift)).WaitAsync(Rundtak, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            rundan.Cancel();

            var kvar = due.Where(d => !d.Uppgift.IsCompleted).Select(d => d.Kalla).ToList();

            logger.LogError(
                "Rundan blev inte klar inom {Tak} och lämnas därhän. Källor som inte svarade: {Kallor}. Övriga källor fortsätter uppdateras.",
                Rundtak, kvar.Count > 0 ? string.Join(", ", kvar) : "inga");
        }
    }
    private void ResetScheduleIfSettingsChanged()
    {
        var current = options.CurrentValue;

        if (ReferenceEquals(lastSeenSettings, current))
        {
            return;
        }

        lastSeenSettings = current;
        lastAttempt.Clear();
    }

    private void Forget(string source) => lastAttempt.TryRemove(source, out _);
    private bool Due(string source, TimeSpan interval)
    {
        var floor = IntervalLimits.Clamp(source, interval);
        var now = DateTimeOffset.UtcNow;

        if (lastAttempt.TryGetValue(source, out var at) && now - at < floor)
        {
            return false;
        }

        lastAttempt[source] = now;
        return true;
    }
    private async Task PublishAsync<T>(Func<IServiceProvider, DashboardSnapshot, CancellationToken, Task<Fetched<T>>> fetch, Func<DashboardSnapshot, T, DashboardSnapshot> apply, string source, IServiceProvider provider, CancellationToken ct)
    {
        try
        {
            var result = await fetch(provider, state.Current, ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            state.Update(snapshot =>
            {
                var updated = apply(snapshot, result.Value);

                return result.Succeeded ? updated with { LastSuccessUtc = updated.LastSuccessUtc.SetItem(source, now) } : updated;
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) {}
        catch (Exception ex)
        {
            logger.LogError(ex, "Källan {Source} kunde inte uppdateras.", source);
        }
    }

    private async Task<Fetched<ImmutableDictionary<string, CityWeather>>> RefreshWeatherAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        if (settings.OpenWeather.Cities.Count == 0)
        {
            Forget(WeatherSource);
            return new(previous.Cities, false);
        }

        var client = provider.GetRequiredService<OpenWeatherClient>();
        var results = await Task.WhenAll(settings.OpenWeather.Cities.Select(city => client.GetWeatherAsync(city, ct))).ConfigureAwait(false);
        var builder = previous.Cities.ToBuilder();
        var anySucceeded = false;

        foreach (var weather in results)
        {
            if (weather is not null)
            {
                builder[weather.Key] = weather;
                anySucceeded = true;
            }
        }

        return new(builder.ToImmutable(), anySucceeded);
    }

    private async Task<Fetched<NameDayInfo?>> RefreshNameDayAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        if (!settings.NameDay.Enabled)
        {
            Forget(NameDaySource);
            return new(null, false);
        }

        var today = DateOnly.FromDateTime(SwedishTime.Now);
        var client = provider.GetRequiredService<NameDayClient>();
        var result = await client.GetNameDayAsync(today, ct).ConfigureAwait(false);

        return result is null? new(previous.NameDay, false): new(result, true);
    }

    private async Task<Fetched<FuelState>> RefreshFuelAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        var current = new FuelState(previous.FuelPrices, previous.FuelMunicipalities);

        if (!settings.GasolinePrice.Enabled || string.IsNullOrWhiteSpace(settings.GasolinePrice.Url))
        {
            Forget(FuelSource);
            return new(new FuelState([], []), false);
        }

        var client = provider.GetRequiredService<GasolinePriceClient>();
        var report = await client.GetPricesAsync(settings.GasolinePrice.Url, settings.GasolinePrice.Municipalities, ct).ConfigureAwait(false);

        return report is null ? new(current, false) : new(new FuelState(report.Cheapest, report.Municipalities), true);
    }

    private async Task<Fetched<ElectricityPrices?>> RefreshElectricityAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        if (!settings.ElectricityPrice.Enabled)
        {
            Forget(ElectricitySource);
            return new(null, false);
        }

        var today = DateOnly.FromDateTime(SwedishTime.Now);
        var client = provider.GetRequiredService<ElectricityPriceClient>();
        var prices = await client.GetPricesAsync(settings.ElectricityPrice.Zone, today, ct).ConfigureAwait(false);

        return prices is null? new(previous.Electricity, false): new(prices, true);
    }

    private async Task<Fetched<ImmutableArray<NewsItem>>> RefreshNewsAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        if (!settings.News.Enabled || string.IsNullOrWhiteSpace(settings.News.Url))
        {
            Forget(NewsSource);
            return new([], false);
        }

        var client = provider.GetRequiredService<NewsClient>();
        var items = await client.GetHeadlinesAsync(settings.News.Url, settings.News.MaxItems, ct).ConfigureAwait(false);

        return items.IsEmpty ? new(previous.News, false) : new(items, true);
    }

    private async Task<Fetched<ImmutableArray<NewsItem>>> RefreshFinanceNewsAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        var feed = settings.FinanceNews;
        if (!feed.Enabled || string.IsNullOrWhiteSpace(feed.Url))
        {
            Forget(FinanceNewsSource);
            return new([], false);
        }

        var client = provider.GetRequiredService<NewsClient>();
        var items = await client.GetHeadlinesAsync(feed.Url, feed.MaxItems, ct).ConfigureAwait(false);

        return items.IsEmpty ? new(previous.FinanceNews, false) : new(items, true);
    }

    private async Task<Fetched<ImmutableArray<InterestRate>>> RefreshRatesAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        var feed = settings.InterestRates;

        if (!feed.Enabled || feed.Series.Count == 0)
        {
            Forget(RatesSource);
            return new([], false);
        }
        var client = provider.GetRequiredService<RiksbankClient>();
        var rates = await client.GetRatesAsync(feed.Series, ct).ConfigureAwait(false);

        return rates.IsEmpty ? new(previous.Rates, false) : new(rates, true);
    }

    private async Task<Fetched<ImmutableArray<StockQuote>>> RefreshStocksAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        var feed = settings.Stocks;
        var client = provider.GetRequiredService<FinnhubClient>();

        if (!feed.Enabled || feed.Symbols.Count == 0 || !client.IsEnabled)
        {
            Forget(StocksSource);
            return new([], false);
        }

        var quotes = await client.GetQuotesAsync(feed.Symbols, ct).ConfigureAwait(false);
        return quotes.Any(q => q.Price is not null)  ? new(quotes, true) : new(previous.Stocks.IsEmpty ? quotes : previous.Stocks, false);
    }

    private async Task<Fetched<ImmutableArray<CalendarEvent>>> RefreshCalendarAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        var feed = settings.Calendar;

        if (!feed.Enabled || feed.Feeds.Count == 0)
        {
            Forget(CalendarSource);
            return new([], false);
        }

        var client = provider.GetRequiredService<CalendarClient>();
        var events = await client.GetEventsAsync(feed, ct).ConfigureAwait(false);

        return events.IsEmpty && !previous.Events.IsEmpty ? new(previous.Events, false) : new(events, true);
    }

    private async Task<Fetched<ImmutableArray<StopBoard>>> RefreshDeparturesAsync(IServiceProvider provider, DashboardSnapshot previous, CancellationToken ct)
    {
        var feed = settings.Transit;
        var client = provider.GetRequiredService<ResRobotClient>();

        if (!feed.Enabled || feed.Stops.Count == 0 || !client.IsEnabled)
        {
            Forget(DeparturesSource);
            return new([], false);
        }

        var boards = await client.GetBoardsAsync(feed.Stops, ct).ConfigureAwait(false);
        return new(boards, !boards.IsEmpty);
    }

    private async Task<Fetched<ImmutableArray<SchoolMenu>>> RefreshSchoolMenuAsync(IServiceProvider provider, DashboardSnapshot previous,CancellationToken ct)
    {
        var feed = settings.SchoolMenu;
        var source = provider.GetRequiredService<SchoolMenuService>();

        if (!feed.Enabled || feed.Schools.Count == 0 || !source.IsEnabled)
        {
            Forget(SchoolMenuSource);
            return new([], false);
        }

        var menus = await source.GetMenusAsync(feed.Schools, ct).ConfigureAwait(false);
        var (varde, lyckades) = BehallOmTomt(menus, previous.Menus);

        return new(varde, lyckades);
    }

    internal static (ImmutableArray<SchoolMenu> Value, bool Succeeded) BehallOmTomt(ImmutableArray<SchoolMenu> nya, ImmutableArray<SchoolMenu> tidigare) =>
        nya.IsEmpty && !tidigare.IsEmpty ? (tidigare, false) : (nya, !nya.IsEmpty);

    private async Task<Fetched<ImmutableDictionary<string, IndoorReading>>> RefreshIndoorAsync(IServiceProvider provider,DashboardSnapshot previous, CancellationToken ct)
    {
        var client = provider.GetRequiredService<ShellyClient>();

        if (!client.IsEnabled || settings.Shelly.Sensors.Count == 0)
        {
            Forget(IndoorSource);
            return new(ImmutableDictionary<string, IndoorReading>.Empty, false);
        }

        var results = await Task.WhenAll(settings.Shelly.Sensors.Select(sensor => client.GetSensorAsync(sensor, ct))).ConfigureAwait(false);

        var builder = previous.Indoor.ToBuilder();
        var anySucceeded = false;

        foreach (var reading in results)
        {
            if (reading is not null)
            {
                builder[reading.Key] = reading;
                anySucceeded = true;
            }
        }

        return new(builder.ToImmutable(), anySucceeded);
    }
}
