using System.Security.Cryptography;
using System.Text.Json;

namespace BLZULSA.Services;

public enum PinOutcome
{
    Unlocked,
    Wrong,
    Locked,
    NotSet,
}

public readonly record struct PinAttempt(PinOutcome Outcome, int AttemptsLeft);

public sealed class PinGuard
{
    public const int Length = 4;
    public const int MaxAttempts = 3;
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(10);

    private const string AlgorithmName = "PBKDF2-SHA256";
    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock sync = new();
    private readonly string pinPath;
    private readonly string statePath;
    private readonly TimeProvider clock;
    private readonly ILogger<PinGuard> logger;
    private LockState state;

    public PinGuard(string dataDirectory, TimeProvider clock, ILogger<PinGuard> logger)
    {
        pinPath = PinPathIn(dataDirectory);
        statePath = Path.Combine(dataDirectory, "pin-lock.json");
        this.clock = clock;
        this.logger = logger;
        state = LoadState();
    }

    public event Action? LockedOut;

    public bool IsSet => File.Exists(pinPath);

    public DateTimeOffset? LockedUntil
    {
        get
        {
            lock (sync)
            {
                ExpireIfDue();
                return state.LockedUntil;
            }
        }
    }

    public static string PinPathIn(string dataDirectory) => Path.Combine(dataDirectory, "pin.json");

    public static bool IsWellFormed(string? pin) => pin is { Length: Length } && pin.All(char.IsAsciiDigit);

    public PinAttempt TryUnlock(string pin)
    {
        lock (sync)
        {
            ExpireIfDue();

            if (state.LockedUntil is not null)
            {
                return new(PinOutcome.Locked, 0);
            }

            if (ReadPin() is not { } stored)
            {
                return new(PinOutcome.NotSet, MaxAttempts - state.Failures);
            }

            if (IsWellFormed(pin) && Matches(pin, stored))
            {
                if (state != default)
                {
                    state = default;
                    SaveState();
                }

                return new(PinOutcome.Unlocked, MaxAttempts);
            }

            var failures = state.Failures + 1;

            if (failures < MaxAttempts)
            {
                state = new LockState(failures, null);
                SaveState();
                return new(PinOutcome.Wrong, MaxAttempts - failures);
            }

            state = new LockState(0, clock.GetUtcNow() + Lockout);
            SaveState();
            logger.LogWarning("Inställningarna låstes i {Minutes} minuter efter {Attempts} felaktiga PIN-försök.", Lockout.TotalMinutes, MaxAttempts);
        }

        LockedOut?.Invoke();
        return new(PinOutcome.Locked, 0);
    }

    public static void WritePin(string path, string pin)
    {
        if (!IsWellFormed(pin))
        {
            throw new ArgumentException("PIN måste vara fyra siffror.", nameof(pin));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        var stored = new StoredPin(AlgorithmName, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));

        WriteRestricted(path, JsonSerializer.Serialize(stored, Json));
    }

    private void ExpireIfDue()
    {
        if (state.LockedUntil is { } until && clock.GetUtcNow() >= until)
        {
            state = default;
            SaveState();
        }
    }

    private LockState LoadState()
    {
        if (!File.Exists(statePath))
        {
            return default;
        }

        try
        {
            using var stream = File.OpenRead(statePath);
            return JsonSerializer.Deserialize<LockState>(stream, Json);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "PIN-spärren gick inte att läsa — låser Inställningar för säkerhets skull.");
            return new LockState(0, clock.GetUtcNow() + Lockout);
        }
    }

    private void SaveState()
    {
        try
        {
            if (state == default)
            {
                File.Delete(statePath);
                return;
            }

            WriteRestricted(statePath, JsonSerializer.Serialize(state, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Kunde inte spara PIN-spärren till {Path}.", statePath);
        }
    }

    private StoredPin? ReadPin()
    {
        try
        {
            using var stream = File.OpenRead(pinPath);
            return JsonSerializer.Deserialize<StoredPin>(stream, Json);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "PIN-filen {Path} går inte att läsa.", pinPath);
            return null;
        }
    }

    private static bool Matches(string pin, StoredPin stored)
    {
        if (stored.Algorithm != AlgorithmName || stored.Iterations <= 0)
        {
            return false;
        }

        byte[] salt, expected;

        try
        {
            salt = Convert.FromBase64String(stored.Salt);
            expected = Convert.FromBase64String(stored.Hash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, stored.Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static void WriteRestricted(string path, string contents)
    {
        var temporary = path + ".tmp";
        File.Delete(temporary);

        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var writer = new StreamWriter(temporary, options))
        {
            writer.Write(contents);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private readonly record struct LockState(int Failures, DateTimeOffset? LockedUntil);

    private sealed record StoredPin(string Algorithm, int Iterations, string Salt, string Hash);
}
