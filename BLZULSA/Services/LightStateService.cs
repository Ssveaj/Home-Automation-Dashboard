using BLZULSA.Clients;
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Services;

public sealed class LightStateService(IServiceScopeFactory scopeFactory, DashboardState state, LightTruth truth, IOptionsMonitor<DashboardOptions> options, ILogger<LightStateService> logger) : BackgroundService
{
    private ShellyOptions Settings => options.CurrentValue.Shelly;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogError(ex, "Oväntat fel vid avläsning av lampor.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private TimeSpan Interval
    {
        get
        {
            var configured = options.CurrentValue.Intervals.Lights;

            return IntervalLimits.Clamp(DashboardRefreshService.LightsSource, configured);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ShellyClient>();

        var lights = Settings.Lights.Where(l => !string.IsNullOrWhiteSpace(l.DeviceId)).ToList();
        if (!client.IsEnabled || lights.Count == 0)
        {
            return;
        }

        var nagotSvar = false;

        foreach (var light in lights)
        {
            if (await MedTakAsync(t => client.GetLightStateAsync(light, t), light.Name, ct).ConfigureAwait(false) is not { } avlast)
            {
                continue;
            }

            nagotSvar = true;

            var visat = state.Current.Lights.TryGetValue(light.Key, out var kant) ? kant : null;

            var skaVisas = truth.Avlast(avlast);

            state.Update(snapshot => snapshot with
            {
                Lights = snapshot.Lights.SetItem(skaVisas.Key, skaVisas),
            });

            if (!skaVisas.IsOn && skaVisas.Online && visat is { IsOn: true })
            {
                var ra = await MedTakAsync(t => client.RaStatusAsync(light, t), light.Name, ct).ConfigureAwait(false);
                logger.LogInformation("Avläsningen släckte {Light} utan att någon tryckt. Shelly svarade: {Svar}", light.Name, ra);
            }
        }

        if (!nagotSvar)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        state.Update(snapshot => snapshot with
        {
            LastSuccessUtc = snapshot.LastSuccessUtc.SetItem(DashboardRefreshService.LightsSource, now),
        });
    }

    private static readonly TimeSpan LampTak = TimeSpan.FromSeconds(60);

    private async Task<T?> MedTakAsync<T>(Func<CancellationToken, Task<T?>> anrop, string lampa, CancellationToken ct)
        where T : class
    {
        using var tak = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            return await anrop(tak.Token).WaitAsync(LampTak, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            tak.Cancel();

            logger.LogError("{Light} svarade inte inom {Tak} och hoppas över. Övriga lampor läses som vanligt.", lampa, LampTak);

            return null;
        }
    }
}
