using System.Text.Json;
using System.Text.Json.Serialization;
using BLZULSA.Configuration;
using Microsoft.Extensions.Options;

namespace BLZULSA.Services;
public sealed class SettingsStore(IOptionsMonitor<DashboardOptions> options, ISettingsFileLocator locator, ILogger<SettingsStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new TimeSpanConverter() },
    };
    private readonly SemaphoreSlim writeLock = new(1, 1);
    public string FilePath => locator.FilePath;

    public DateTime LastWriteUtc => File.Exists(locator.FilePath) ? File.GetLastWriteTimeUtc(locator.FilePath) : DateTime.MinValue;
    public DashboardOptions Current => options.CurrentValue;
    public async Task SaveAsync(DashboardOptions settings, CancellationToken ct = default)
    {
        await writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var directory = Path.GetDirectoryName(locator.FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = new SettingsFile { Dashboard = settings };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var temporary = locator.FilePath + ".tmp";

            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            await using (var writer = new StreamWriter(temporary, options))
            {
                await writer.WriteAsync(json.AsMemory(), ct).ConfigureAwait(false);
            }

            if (File.Exists(locator.FilePath))
            {
                try
                {
                    File.Copy(locator.FilePath, locator.FilePath + ".bak", overwrite: true);
                }
                catch (IOException copyFailed)
                {
                    logger.LogWarning(copyFailed, "Kunde inte skriva säkerhetskopia av inställningarna.");
                }
            }

            File.Move(temporary, locator.FilePath, overwrite: true);

            logger.LogInformation("Inställningar sparade till {Path}", locator.FilePath);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private sealed class SettingsFile
    {
        public DashboardOptions Dashboard { get; set; } = new();
    }
    private sealed class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __) => TimeSpan.TryParse(reader.GetString(), out var value) ? value : TimeSpan.Zero;
        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions _) => writer.WriteStringValue(value.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
    }
}

public interface ISettingsFileLocator
{
    string FilePath { get; }
}

public sealed class SettingsFileLocator(string filePath) : ISettingsFileLocator
{
    public string FilePath { get; } = filePath;
}
