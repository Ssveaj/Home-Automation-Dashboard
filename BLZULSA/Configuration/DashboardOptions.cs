using System.ComponentModel.DataAnnotations;

namespace BLZULSA.Configuration;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";
    [Required]
    public string HomeName { get; set; } = "Hemma";
    public bool TouchKeyboard { get; set; }
    [Required]
    public IntervalOptions Intervals { get; set; } = new();
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(15);

    [Required]
    public OpenWeatherOptions OpenWeather { get; set; } = new();

    [Required]
    public NameDayOptions NameDay { get; set; } = new();

    [Required]
    public ClockOptions Clock { get; set; } = new();

    [Required]
    public GasolinePriceOptions GasolinePrice { get; set; } = new();

    [Required]
    public ElectricityPriceOptions ElectricityPrice { get; set; } = new();

    [Required]
    public NewsOptions News { get; set; } = new();

    [Required]
    public InterestRatesOptions InterestRates { get; set; } = new();

    [Required]
    public StocksOptions Stocks { get; set; } = new();

    [Required]
    public NewsOptions FinanceNews { get; set; } = new()
    {
        Url = "https://www.placera.se/artiklar/rss.xml",
        Title = "Placera",
        MaxItems = 5,
    };

    [Required]
    public CalendarOptions Calendar { get; set; } = new();

    [Required]
    public TransitOptions Transit { get; set; } = new();

    [Required]
    public SchoolMenuOptions SchoolMenu { get; set; } = new();

    public ShellyOptions Shelly { get; set; } = new();
}
public sealed class IntervalOptions
{
    public TimeSpan Weather { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan NameDay { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan News { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan FinanceNews { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan Electricity { get; set; } = TimeSpan.FromMinutes(60);
    public TimeSpan Fuel { get; set; } = TimeSpan.FromMinutes(60);
    public TimeSpan InterestRates { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan Stocks { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan Calendar { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan Departures { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan SchoolMenu { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan Lights { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan IndoorSensors { get; set; } = TimeSpan.FromMinutes(10);
}
public sealed class OpenWeatherOptions
{
    [Required]
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5/";
    public string ApiKey { get; set; } = string.Empty;
    public List<CityOptions> Cities { get; set; } = [];
}
public sealed class CityOptions
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Query { get; set; } = string.Empty;

    [Required]
    public string TimeZone { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
public sealed class NameDayOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string BaseUrl { get; set; } = "https://sholiday.faboul.se/dagar/v2.1/";
}
public sealed class GasolinePriceOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string Url { get; set; } = string.Empty;
    public List<string> Municipalities { get; set; } = [];
}
public sealed class ElectricityPriceOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string BaseUrl { get; set; } = "https://www.elprisetjustnu.se/";

    [Required]
    [RegularExpression("^SE[1-4]$", ErrorMessage = "Zone måste vara SE1, SE2, SE3 eller SE4.")]
    public string Zone { get; set; } = "SE3";
}
public sealed class InterestRatesOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string BaseUrl { get; set; } = "https://api.riksbank.se/swea/v1/";
    public List<InterestRateSeries> Series { get; set; } = [];
}
public sealed class StocksOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string BaseUrl { get; set; } = "https://finnhub.io/api/v1/";
    public string ApiKey { get; set; } = string.Empty;
    public List<StockOptions> Symbols { get; set; } = [];
}
public sealed class StockOptions
{
    public string Symbol { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
public sealed class CalendarOptions
{
    public bool Enabled { get; set; } = true;
    public int DaysAhead { get; set; } = 14;
    public int MaxItems { get; set; } = 12;
    public List<CalendarFeedOptions> Feeds { get; set; } = [];
}
public sealed class CalendarFeedOptions
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
public sealed class TransitOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string BaseUrl { get; set; } = "https://api.resrobot.se/v2.1/";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxDepartures { get; set; } = 6;
    public int WindowMinutes { get; set; } = 60;
    public List<TransitStopOptions> Stops { get; set; } = [];
}
public sealed class TransitStopOptions
{
    public string StopId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
public sealed class SchoolMenuOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string PageUrl { get; set; } = "https://skolmaten.se";

    public string FirefoxPath { get; set; } = string.Empty;

    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public List<SchoolOptions> Schools { get; set; } = [];
}
public sealed class SchoolOptions
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
public sealed class ClockOptions
{
    public bool ShowAnalog { get; set; } = true;
}

public sealed class InterestRateSeries
{
    public string SeriesId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
public sealed class NewsOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string Url { get; set; } = "https://www.svt.se/nyheter/lokalt/orebro/rss.xml";
    public string Title { get; set; } = "Nyheter";
    public int MaxItems { get; set; } = 9;
}
public sealed class ShellyOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthKey { get; set; } = string.Empty;
    public List<ShellyDeviceOptions> Sensors { get; set; } = [];
    public List<LightOptions> Lights { get; set; } = [];
}
public sealed class LightOptions
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Icon { get; set; } = "lightbulb";
    public string DeviceId { get; set; } = string.Empty;
    public int Channel { get; set; }

    public bool Dimmer { get; set; }
}

public sealed class ShellyDeviceOptions
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}
