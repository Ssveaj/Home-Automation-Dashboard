using System.Collections.Concurrent;
using BLZULSA.Models;

namespace BLZULSA.Services;

public sealed class LightTruth(TimeProvider clock)
{
    public static readonly TimeSpan Talamod = TimeSpan.FromSeconds(30);

    private const int Tolerans = 3;

    private readonly ConcurrentDictionary<string, Onskan> onskat = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LightState> rapporterat = new(StringComparer.Ordinal);

    public LightState Onskar(string key, bool on, int? brightness)
    {
        rapporterat.TryGetValue(key, out var nuvarande);

        var lage = new LightState(key, on, Online: true, brightness ?? nuvarande?.Brightness);

        onskat[key] = new Onskan(lage, clock.GetUtcNow());
        return lage;
    }

    public LightState Avlast(LightState nytt)
    {
        rapporterat[nytt.Key] = nytt;

        if (!onskat.TryGetValue(nytt.Key, out var onskan))
        {
            return nytt;
        }

        if (Samma(onskan.Lage, nytt) || clock.GetUtcNow() - onskan.Nar >= Talamod)
        {
            onskat.TryRemove(nytt.Key, out _);
            return nytt;
        }

        return onskan.Lage;
    }

    public LightState? Angrar(string key)
    {
        onskat.TryRemove(key, out _);
        return rapporterat.TryGetValue(key, out var kant) ? kant : null;
    }

    private static bool Samma(LightState a, LightState b) =>
        a.IsOn == b.IsOn && a.Online == b.Online && NivaStammer(a.Brightness, b.Brightness);

    private static bool NivaStammer(int? a, int? b) =>
        a is not { } forsta || b is not { } andra || Math.Abs(forsta - andra) <= Tolerans;

    private sealed record Onskan(LightState Lage, DateTimeOffset Nar);
}
