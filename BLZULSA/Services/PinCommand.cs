namespace BLZULSA.Services;

internal static class PinCommand
{
    public static int Run(string dataDirectory)
    {
        if (!OperatingSystem.IsWindows() && Environment.IsPrivilegedProcess)
        {
            Console.Error.WriteLine("Kör inte med sudo. Filen måste ägas av användaren som kör tjänsten, annars kan appen inte läsa den.");
            return 2;
        }

        if (!Directory.Exists(dataDirectory))
        {
            Console.Error.WriteLine($"Hittar ingen datakatalog i {dataDirectory}. Kör kommandot från appens katalog: cd /opt/ulsavilagatan");
            return 2;
        }

        var first = ReadDigits("Ny PIN (4 siffror): ");
        if (!PinGuard.IsWellFormed(first))
        {
            Console.Error.WriteLine("Koden måste vara fyra siffror. Inget sparat.");
            return 1;
        }

        if (ReadDigits("Upprepa: ") != first)
        {
            Console.Error.WriteLine("Koderna var inte samma. Inget sparat.");
            return 1;
        }

        var path = PinGuard.PinPathIn(dataDirectory);
        PinGuard.WritePin(path, first);
        Console.WriteLine($"PIN sparad i {path}. Den gäller direkt, utan omstart.");
        return 0;
    }

    private static string ReadDigits(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        var digits = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace && digits.Length > 0)
            {
                digits.Length--;
                Console.Write("\b \b");
            }
            else if (char.IsAsciiDigit(key.KeyChar) && digits.Length < PinGuard.Length)
            {
                digits.Append(key.KeyChar);
                Console.Write('*');
            }
        }

        Console.WriteLine();
        return digits.ToString();
    }
}
