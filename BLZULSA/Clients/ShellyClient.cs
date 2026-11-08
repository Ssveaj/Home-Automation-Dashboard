using System.Diagnostics;
using System.Text.Json;
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Clients;


public sealed class ShellyClient(HttpClient http, IOptionsMonitor<DashboardOptions> options, ShellyRateLimiter takt, ILogger<ShellyClient> logger)
{
    private ShellyOptions Settings => options.CurrentValue.Shelly;
    public bool IsEnabled => Settings.Enabled && !string.IsNullOrWhiteSpace(Settings.BaseUrl);
    public const int LagstaNivan = 5;

    public async Task<ShellyResult> SetAsync(LightOptions light, bool on, int? brightness, CancellationToken ct)
    {
        var (key, problem) = Prepare(light.DeviceId);
        if (problem is not null)
        {
            return ShellyResult.Fail(problem);
        }

        var falt = new List<KeyValuePair<string, string>>
        {
            new("id", light.DeviceId),
            new("auth_key", key),
            new("channel", light.Channel.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("turn", on ? "on" : "off"),
        };

        if (light.Dimmer && brightness is { } niva)
        {
            falt.Add(new("brightness", Math.Clamp(niva, LagstaNivan, 100).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var vag = light.Dimmer ? "device/light/control" : "device/relay/control";
        var (document, error) = await PostAsync(vag, [.. falt], ct, bradskande: true).ConfigureAwait(false);

        using (document)
        {
            if (error is not null)
            {
                logger.LogWarning("Kunde inte {Action} {Light}: {Error}", on ? "tända" : "släcka", light.Name, error);
                return ShellyResult.Fail(error);
            }
        }

        logger.LogInformation("{Light} {Action}.", light.Name, on ? "tänd" : "släckt");
        return ShellyResult.Success;
    }
    public async Task<LightState?> GetLightStateAsync(LightOptions light, CancellationToken ct)
    {
        var (key, problem) = Prepare(light.DeviceId);
        if (problem is not null)
        {
            return null;
        }

        var (document, error) = await PostAsync("device/status", [new("id", light.DeviceId), new("auth_key", key)], ct).ConfigureAwait(false);

        using (document)
        {
            if (error is not null || document is null)
            {
                return null;
            }

            var root = document.RootElement;
            var online = !root.TryGetProperty("data", out var data) || !data.TryGetProperty("online", out var isOnline) || isOnline.ValueKind != JsonValueKind.False;

            if (ReadState(data, light.Channel) is not { } lage)
            {
                logger.LogInformation("Kunde inte läsa läget för {Light} — behåller det vi visar.", light.Name);
                return null;
            }

            return new LightState(light.Key, lage.On, online, lage.Brightness);
        }
    }
    public async Task<string?> RaStatusAsync(LightOptions light, CancellationToken ct)
    {
        var (key, problem) = Prepare(light.DeviceId);
        if (problem is not null)
        {
            return problem;
        }

        var (document, error) = await PostAsync("device/status", [new("id", light.DeviceId), new("auth_key", key)], ct).ConfigureAwait(false);

        using (document)
        {
            if (error is not null || document is null)
            {
                return error ?? "inget svar";
            }

            var text = document.RootElement.TryGetProperty("data", out var data) ? data.ToString() : document.RootElement.ToString();

            return text.Length > 600 ? text[..600] + "…" : text;
        }
    }

    private static (bool On, int? Brightness)? ReadState(JsonElement data, int channel)
    {
        if (!data.TryGetProperty("device_status", out var status))
        {
            return null;
        }

        var kanal = channel.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (status.TryGetProperty("light:" + kanal, out var ljus))
        {
            return (ljus.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.True, Niva(ljus));
        }

        if (status.TryGetProperty("lights", out var lights) && lights.ValueKind == JsonValueKind.Array && channel < lights.GetArrayLength())
        {
            var gen1 = lights[channel];
            return (gen1.TryGetProperty("ison", out var tand) && tand.ValueKind == JsonValueKind.True, Niva(gen1));
        }

        if (status.TryGetProperty("relays", out var relays) && relays.ValueKind == JsonValueKind.Array && channel < relays.GetArrayLength() && relays[channel].TryGetProperty("ison", out var ison))
        {
            return (ison.ValueKind == JsonValueKind.True, null);
        }

        if (status.TryGetProperty("switch:" + kanal, out var gen2) && gen2.TryGetProperty("output", out var switchOutput))
        {
            return (switchOutput.ValueKind == JsonValueKind.True, null);
        }

        return null;
    }

    private static int? Niva(JsonElement element) =>
        element.TryGetProperty("brightness", out var value) && value.ValueKind == JsonValueKind.Number
            ? Math.Clamp((int)Math.Round(value.GetDouble()), 0, 100)
            : null;

    public async Task<IndoorReading?> GetSensorAsync(ShellyDeviceOptions device, CancellationToken ct)
    {
        var (key, problem) = Prepare(device.DeviceId);
        if (problem is not null)
        {
            return null;
        }

        var (document, error) = await PostAsync("device/status", [new("id", device.DeviceId), new("auth_key", key)], ct).ConfigureAwait(false);
        using (document)
        {
            if (error is not null || document is null)
            {
                if (error is not null)
                {
                    logger.LogWarning("Shelly {Device}: {Error}", device.DisplayName, error);
                }

                return null;
            }

            if (!document.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("device_status", out var status))
            {
                return null;
            }

            return new IndoorReading(device.Key, device.DisplayName, (decimal)ReadNumber(status, "tmp", "tC"),ReadNumber(status, "hum", "value"), (int)ReadNumber(status, "bat", "value"));
        }
    }
    public async Task<ShellyResult> TestDeviceAsync(string deviceId, string apiKey, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return ShellyResult.Fail("Shelly är avstängt — kryssa i Aktivera.");
        }

        var (key, problem) = Prepare(deviceId);
        if (problem is not null)
        {
            return ShellyResult.Fail(problem);
        }

        var (document, error) = await PostAsync("device/status", [new("id", deviceId), new("auth_key", key)], ct).ConfigureAwait(false);

        using (document)
        {
            if (error is not null)
            {
                return ShellyResult.Fail(error);
            }

            var online = document is not null && document.RootElement.TryGetProperty("data", out var data) && (!data.TryGetProperty("online", out var isOnline) || isOnline.ValueKind != JsonValueKind.False);

            return online ? ShellyResult.Success : ShellyResult.Fail("Enheten svarar men är offline.");
        }
    }

    private (string Key, string? Problem) Prepare(string deviceId)
    {
        if (!IsEnabled)
        {
            return (string.Empty, "Shelly är inte aktiverat.");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return (string.Empty, "Enhets-ID saknas.");
        }

        var key = Settings.AuthKey;

        return string.IsNullOrWhiteSpace(key) ? (string.Empty, "API-nyckel saknas.") : (key, null);
    }

    private async Task<(JsonDocument? Document, string? Error)> PostAsync(string path, KeyValuePair<string, string>[] fields, CancellationToken ct, bool bradskande = false)
    {
        try
        {
            var start = Stopwatch.GetTimestamp();
            await takt.WaitAsync(ct, bradskande).ConfigureAwait(false);
            var ko = Stopwatch.GetElapsedTime(start);

            using var content = new FormUrlEncodedContent(fields);
            var natet = Stopwatch.GetTimestamp();
            var response = await http.PostAsync(path, content, ct).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (bradskande || logger.IsEnabled(LogLevel.Debug))
            {
                logger.Log(bradskande ? LogLevel.Information : LogLevel.Debug,
                    "Shelly {Path}: {Ko} ms kö + {Nat} ms moln = {Allt} ms{Brads}",
                    path,
                    (int)ko.TotalMilliseconds,
                    (int)Stopwatch.GetElapsedTime(natet).TotalMilliseconds,
                    (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                    bradskande ? " (tryck)" : string.Empty);
            }
            if (!response.IsSuccessStatusCode)
            {
                return (null, DescribeStatus(response.StatusCode, body));
            }

            var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("isok", out var isok) && isok.ValueKind == JsonValueKind.False)
            {
                var reason = DescribeError(document.RootElement);
                document.Dispose();
                return (null, reason);
            }

            return (document, null);
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException
            or Polly.Timeout.TimeoutRejectedException
            or Polly.CircuitBreaker.BrokenCircuitException)
        {
            return (null, "Kunde inte nå Shelly Cloud.");
        }
        catch (JsonException)
        {
            return (null, "Oväntat svar från Shelly Cloud.");
        }
    }
    private static string DescribeStatus(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("errors", out _))
            {
                return DescribeError(document.RootElement);
            }
        }
        catch (JsonException){}

        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "Nyckeln godkändes inte — skapa en ny auth key i Shelly Cloud.",
            System.Net.HttpStatusCode.NotFound => "Adressen till Shelly Cloud stämmer inte — kontrollera serverns namn.",
            _ => $"Shelly Cloud svarade {(int)status}.",
        };
    }
    private static string DescribeError(JsonElement root)
    {
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
        {
            foreach (var error in errors.EnumerateObject())
            {
                return error.Name switch
                {
                    "invalid_token" or "invalid_header" or "not_authorized" => "Nyckeln godkändes inte — kontrollera att hela auth key:n klistrats in.",
                    "device_not_found" or "wrong_device_id" => "Enhets-ID:t finns inte på kontot — kontrollera de tolv tecknen.",
                    "device_offline" => "Enheten är offline.",
                    _ => $"Shelly svarade: {error.Name}.",
                };
            }
        }

        return "Shelly avvisade anropet.";
    }

    private static double ReadNumber(JsonElement parent, string objectName, string field) => parent.TryGetProperty(objectName, out var child) && child.TryGetProperty(field, out var value)  
                                                                                                                                              && value.ValueKind == JsonValueKind.Number
                                                                                                                                              ? value.GetDouble(): 0;
}
