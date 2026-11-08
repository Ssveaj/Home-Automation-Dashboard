namespace BLZULSA.Clients;

public sealed class ShellyRateLimiter(TimeProvider clock)
{
    public static readonly TimeSpan MinstaLucka = TimeSpan.FromSeconds(1.1);

    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset senaste = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken ct, bool bradskande = false)
    {
        if (bradskande)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                senaste = clock.GetUtcNow();
            }
            finally
            {
                gate.Release();
            }

            return;
        }

        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var sedan = clock.GetUtcNow() - senaste;

            if (sedan < MinstaLucka)
            {
                await Task.Delay(MinstaLucka - sedan, clock, ct).ConfigureAwait(false);
            }

            senaste = clock.GetUtcNow();
        }
        finally
        {
            gate.Release();
        }
    }
}
