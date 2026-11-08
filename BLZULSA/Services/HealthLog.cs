namespace BLZULSA.Services;

public sealed record HealthIncident(DateTimeOffset At, string Source, bool IsError, string Message);
public sealed class HealthLog
{
    private const int Capacity = 200;
    private readonly Lock gate = new();
    private readonly Queue<HealthIncident> incidents = new(Capacity);
    public event Action? Changed;
    public IReadOnlyList<HealthIncident> Recent
    {
        get
        {
            lock (gate)
            {
                return [.. incidents.Reverse()];
            }
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return incidents.Count;
            }
        }
    }
    public int ErrorCount
    {
        get
        {
            lock (gate)
            {
                return incidents.Count(i => i.IsError);
            }
        }
    }

    public void Record(HealthIncident incident)
    {
        lock (gate)
        {
            if (incidents.Count >= Capacity)
            {
                incidents.Dequeue();
            }

            incidents.Enqueue(incident);
        }

        NotifyChanged();
    }

    public void Clear()
    {
        lock (gate)
        {
            incidents.Clear();
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        var handlers = Changed;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (ObjectDisposedException) { }
        }
    }
}

public sealed class HealthLogProvider(HealthLog log) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new HealthLogger(log, categoryName);
    public void Dispose() => GC.SuppressFinalize(this);
}

internal sealed class HealthLogger(HealthLog log, string category) : ILogger
{
    private const string OurNamespace = "BLZULSA.";
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning && category.StartsWith(OurNamespace, StringComparison.Ordinal);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message = $"{message} — {exception.Message}";
        }

        log.Record(new HealthIncident(DateTimeOffset.UtcNow, FriendlyName(category), logLevel >= LogLevel.Error, message));
    }
    internal static string FriendlyName(string category)
    {
        var shortName = category[(category.LastIndexOf('.') + 1)..];

        return shortName switch
        {
            "OpenWeatherClient" => "Väder",
            "NameDayClient" => "Namnsdag",
            "GasolinePriceClient" => "Drivmedel",
            "ElectricityPriceClient" => "Elpris",
            "NewsClient" => "Nyheter",
            "RiksbankClient" => "Räntor",
            "FinnhubClient" => "Aktier",
            "CalendarClient" => "Kalender",
            "ResRobotClient" => "Bussavgångar",
            "SchoolMenuService" => "Skollunch",
            "ShellyClient" => "Enheter",
            "LightStateService" => "Enheter",
            "SettingsStore" => "Inställningar",
            "DashboardRefreshService" => "Uppdatering",
            _ => shortName,
        };
    }
}
