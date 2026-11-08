namespace BLZULSA.Models;

public sealed record LightState(string Key, bool IsOn, bool Online, int? Brightness = null);

public sealed record ShellyResult(bool Ok, string? Error)
{
    public static readonly ShellyResult Success = new(true, null);
    public static ShellyResult Fail(string error) => new(false, error);
}
