using System.Globalization;

namespace BLZULSA.Services;

public sealed class KioskPause(string pauseFile, TimeProvider clock, ILogger<KioskPause> logger)
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    public bool IsAvailable => Directory.Exists(Path.GetDirectoryName(pauseFile));

    public bool TryPause()
    {
        var until = clock.GetUtcNow() + Duration;

        try
        {
            var temporary = pauseFile + ".tmp";
            File.WriteAllText(temporary, until.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            File.Move(temporary, pauseFile, overwrite: true);

            logger.LogInformation("Tavlan lämnades från Inställningar i {Minutes} minuter.", Duration.TotalMinutes);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Kunde inte lägga pausfilen {Path}.", pauseFile);
            return false;
        }
    }
}
