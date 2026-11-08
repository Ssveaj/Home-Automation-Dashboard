using System.Text.Json.Serialization;

namespace BLZULSA.Models;
public sealed class OpenWeatherResponse
{
    [JsonPropertyName("weather")]
    public List<OpenWeatherCondition> Weather { get; set; } = [];

    [JsonPropertyName("main")]
    public OpenWeatherMain? Main { get; set; }

    [JsonPropertyName("wind")]
    public OpenWeatherWind? Wind { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
public sealed class OpenWeatherCondition
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}
public sealed class OpenWeatherMain
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("pressure")]
    public int Pressure { get; set; }
}
public sealed class OpenWeatherWind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }
}
