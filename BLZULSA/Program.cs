using BLZULSA.Clients;
using BLZULSA.Configuration;
using BLZULSA.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

const string SetPinFlag = "--satt-pin";
var setPin = args.Contains(SetPinFlag, StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder([.. args.Where(a => a != SetPinFlag)]);
builder.Services.AddSystemd();

var settingsPath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["SettingsFile"] ?? Path.Combine("data", "settings.json"));

if (setPin)
{
    Environment.ExitCode = PinCommand.Run(Path.GetDirectoryName(settingsPath)!);
    return;
}

Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
SeedSettings(settingsPath, builder.Environment.ContentRootPath);
EnsureReadable(settingsPath, builder.Environment.ContentRootPath);

builder.Configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: true);
builder.Services.AddSingleton<ISettingsFileLocator>(new SettingsFileLocator(settingsPath));
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new PinGuard(Path.GetDirectoryName(settingsPath)!, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<PinGuard>>()));

var kioskPauseFile = builder.Configuration["KioskPauseFile"] ?? "/run/ulsavilagatan/kiosk-paus";
builder.Services.AddSingleton(sp => new KioskPause(kioskPauseFile, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<KioskPause>>()));
builder.Services.AddSingleton<RefreshTrigger>();
builder.Services.AddOptions<DashboardOptions>().Bind(builder.Configuration.GetSection(DashboardOptions.SectionName)).ValidateDataAnnotations()
    .Validate(o => o.OpenWeather.Cities.Select(c => c.Key)
    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == o.OpenWeather.Cities.Count, "Varje stad under Dashboard:OpenWeather:Cities måste ha en unik Key.").ValidateOnStart();

var dashboardOptions = builder.Configuration.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>() ?? new DashboardOptions();
var attemptTimeout = dashboardOptions.HttpTimeout <= TimeSpan.Zero? TimeSpan.FromSeconds(15): dashboardOptions.HttpTimeout;

static bool VardAttForsokaIgen(Polly.Outcome<HttpResponseMessage> utfall) => utfall switch
{
    { Exception: HttpRequestException or Polly.Timeout.TimeoutRejectedException } => true,
    { Result: { } svar } => (int)svar.StatusCode >= 500,
    _ => false,
};

void ConfigureResilience(Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions o)
{
    o.AttemptTimeout.Timeout = attemptTimeout;
    o.TotalRequestTimeout.Timeout = attemptTimeout * 3;
    o.CircuitBreaker.SamplingDuration = attemptTimeout * 2;
}

builder.Services.AddHttpClient<OpenWeatherClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.OpenWeather.BaseUrl));
        client.Timeout = attemptTimeout * 4;
    }).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<NameDayClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.NameDay.BaseUrl));
        client.Timeout = attemptTimeout * 4;
    }).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<GasolinePriceClient>(client => client.Timeout = attemptTimeout * 4).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<RiksbankClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.InterestRates.BaseUrl));
        client.Timeout = attemptTimeout * 4;
    }).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<FinnhubClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.Stocks.BaseUrl));
        client.Timeout = attemptTimeout * 4;
    }).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<CalendarClient>(client => client.Timeout = attemptTimeout * 4).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<ResRobotClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.Transit.BaseUrl));
        client.Timeout = attemptTimeout * 4;
    }).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<NewsClient>(client => client.Timeout = attemptTimeout * 4).AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<ElectricityPriceClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.ElectricityPrice.BaseUrl));
        client.Timeout = attemptTimeout * 4;
    })
    .AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<ShellyClient>(client =>
    {
        if (!string.IsNullOrWhiteSpace(dashboardOptions.Shelly.BaseUrl))
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(dashboardOptions.Shelly.BaseUrl));
        }

        client.Timeout = attemptTimeout * 4;
    }).AddStandardResilienceHandler(o =>
    {
        ConfigureResilience(o);
        o.Retry.ShouldHandle = args => ValueTask.FromResult(VardAttForsokaIgen(args.Outcome));
        o.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(VardAttForsokaIgen(args.Outcome));
    });

var healthLog = new HealthLog();
builder.Services.AddSingleton(healthLog);
builder.Logging.AddProvider(new HealthLogProvider(healthLog));

builder.Services.AddSingleton<DashboardState>();
builder.Services.AddHostedService<DashboardRefreshService>();
builder.Services.AddSingleton<LightTruth>();
builder.Services.AddSingleton<SchoolMenuService>();
builder.Services.AddSingleton<ShellyRateLimiter>();
builder.Services.AddHostedService<LightStateService>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHealthChecks().AddCheck<DashboardHealthCheck>("dashboard-data");

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/error", "?code={0}");
app.MapStaticAssets();
app.UseAntiforgery();
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapSchoolMenuApi();

app.MapRazorComponents<BLZULSA.App>().AddInteractiveServerRenderMode();
app.Run();

static string EnsureTrailingSlash(string url) => string.IsNullOrWhiteSpace(url) || url.EndsWith('/') ? url : url + "/";
static void EnsureReadable(string settingsPath, string contentRoot)
{
    if (!File.Exists(settingsPath))
    {
        return;
    }

    try
    {
        using var probe = File.OpenRead(settingsPath);
        using var _ = System.Text.Json.JsonDocument.Parse(probe);
        return;
    }
    catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
    {
        var broken = settingsPath + $".trasig-{DateTime.Now:yyyyMMdd-HHmmss}";

        try
        {
            File.Move(settingsPath, broken, overwrite: true);
            Console.Error.WriteLine($"Inställningsfilen gick inte att läsa och flyttades till {broken}.");
        }
        catch (IOException)
        {
            return;
        }

        var backup = settingsPath + ".bak";

        if (File.Exists(backup))
        {
            File.Copy(backup, settingsPath);
            Console.Error.WriteLine("Återställde inställningarna från senaste fungerande kopia.");
            return;
        }

        SeedSettings(settingsPath, contentRoot);
        Console.Error.WriteLine("Ingen kopia fanns — startade om från standardinställningarna.");
    }
}

static void SeedSettings(string settingsPath, string contentRoot)
{
    if (File.Exists(settingsPath))
    {
        return;
    }

    var seed = Path.Combine(contentRoot, "dashboard.defaults.json");

    if (File.Exists(seed))
    {
        File.Copy(seed, settingsPath);
    }
}
internal sealed class DashboardHealthCheck(DashboardState state, IOptionsMonitor<DashboardOptions> options): IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var sources = HealthMonitor.Describe(options.CurrentValue, state.Current);

        var stale = sources.Where(s => s.State == HealthState.Stale).Select(s => s.Name).ToList();
        if (stale.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Tysta källor: {string.Join(", ", stale)}."));
        }

        var waiting = sources.Where(s => s.State == HealthState.Waiting).Select(s => s.Name).ToList();
        if (waiting.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Väntar på första hämtningen: {string.Join(", ", waiting)}."));
        }

        var ok = sources.Count(s => s.State == HealthState.Ok);
        var setup = sources.Count(s => s.State == HealthState.NeedsSetup);

        return Task.FromResult(ok == 0
            ? HealthCheckResult.Degraded("Ingen källa är igång.")
            : HealthCheckResult.Healthy($"{ok} källor aktiva, {setup} behöver inställning."));
    }
}
