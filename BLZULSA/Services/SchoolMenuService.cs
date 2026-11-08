using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using BLZULSA.Configuration;
using BLZULSA.Models;
using Microsoft.Extensions.Options;

namespace BLZULSA.Services;

public sealed class SchoolMenuService(IOptionsMonitor<DashboardOptions> options, ILogger<SchoolMenuService> logger)
{
    private SchoolMenuOptions Settings => options.CurrentValue.SchoolMenu;

    private static readonly string[] Kandidater =
    [
        "/usr/bin/firefox-esr",
        "/usr/bin/firefox",
        @"C:\Program Files\Mozilla Firefoxirefox.exe",
        @"C:\Program Files (x86)\Mozilla Firefoxirefox.exe",
    ];

    public bool IsEnabled => HittaFirefox() is not null;

    public async Task<ImmutableArray<SchoolMenu>> GetMenusAsync(IReadOnlyList<SchoolOptions> schools, CancellationToken ct)
    {
        if (schools.Count == 0)
        {
            return [];
        }

        if (HittaFirefox() is not { } firefox)
        {
            logger.LogWarning("Hittar ingen Firefox att rendera matsedeln med. Letade i: {Platser}. Behåller förra matsedeln.", string.Join(", ", Kandidater));
            return [];
        }

        var (ar, vecka) = Skolveckan();
        var menus = new List<SchoolMenu>(schools.Count);

        foreach (var school in schools)
        {
            var slug = NormalizeSlug(school.Slug);

            if (slug.Length == 0)
            {
                continue;
            }

            if (await FetchWeekAsync(firefox, slug, ar, vecka, ct).ConfigureAwait(false) is not { } days)
            {
                logger.LogWarning("Matsedeln för {School} ({Slug}) kunde inte hämtas. Det som redan visas står kvar.", school.Name, school.Slug);
                continue;
            }

            var name = string.IsNullOrWhiteSpace(school.Name) ? slug : school.Name;
            menus.Add(new SchoolMenu(slug, name, days));
        }

        return [.. menus];
    }

    private static (int Ar, int Vecka) Skolveckan()
    {
        var idag = SwedishTime.Now.Date;

        return (ISOWeek.GetYear(idag), ISOWeek.GetWeekOfYear(idag));
    }

    private async Task<IReadOnlyList<SchoolMenuDay>?> FetchWeekAsync(string firefox, string slug, int ar, int vecka, CancellationToken ct)
    {
        var url = $"{Settings.PageUrl.TrimEnd('/')}/{Uri.EscapeDataString(slug)}?week={vecka}&year={ar}";

        try
        {
            var html = await RenderPageAsync(firefox, url, ct).ConfigureAwait(false);
            var days = ParseMenu(html, ar, vecka);

            logger.LogDebug("Matsedeln för {School} vecka {Week} lästes: {Count} dagar.", slug, vecka, days.Count);

            return days;
        }
        catch (SchoolMenuFormatException ex)
        {
            logger.LogWarning("Matsedeln för {School} vecka {Week} gick inte att tolka: {Fel}", slug, vecka, ex.Message);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Matsedeln för {School} vecka {Week} kunde inte hämtas.", slug, vecka);
            return null;
        }
    }

    private async Task<string> RenderPageAsync(string firefox, string url, CancellationToken ct)
    {
        var profil = Path.Combine(Path.GetTempPath(), "ulsavilagatan-skolmat-" + Guid.NewGuid().ToString("N")[..8]);
        var port = LedigPort();

        Directory.CreateDirectory(profil);
        await File.WriteAllTextAsync(
            Path.Combine(profil, "user.js"),
            "user_pref(\"marionette.port\", " + port.ToString(CultureInfo.InvariantCulture) + ");\n",
            ct).ConfigureAwait(false);

        var start = new ProcessStartInfo(firefox)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var flagga in new[]
        {
            "--headless", "--no-remote", "--profile", profil, "--marionette",
            "--window-size", "1280,1400",
        })
        {
            start.ArgumentList.Add(flagga);
        }

        using var process = new Process { StartInfo = start };

        using var klocka = CancellationTokenSource.CreateLinkedTokenSource(ct);
        klocka.CancelAfter(Settings.RenderTimeout);

        try
        {
            process.Start();

            _ = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            _ = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using var marionette = await AnslutAsync(port, klocka.Token).ConfigureAwait(false);

            await marionette.KommandoAsync("WebDriver:NewSession", "{\"capabilities\":{}}", klocka.Token).ConfigureAwait(false);
            await marionette.KommandoAsync("WebDriver:Navigate", JsonSerializer.Serialize(new { url }), klocka.Token).ConfigureAwait(false);
            await VantaPaSidanAsync(marionette, klocka.Token).ConfigureAwait(false);

            var svar = await marionette.KommandoAsync(
                "WebDriver:ExecuteScript",
                JsonSerializer.Serialize(new { script = "return document.documentElement.outerHTML;", args = Array.Empty<string>() }),
                klocka.Token).ConfigureAwait(false);

            return svar.TryGetProperty("value", out var varde) ? varde.GetString() ?? string.Empty : string.Empty;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Firefox hann inte rendera " + url + " inom " + Settings.RenderTimeout + ".");
        }
        finally
        {
            Doda(process);
            Stada(profil);
        }
    }

    private static async Task VantaPaSidanAsync(Marionette marionette, CancellationToken ct)
    {
        var fraga = JsonSerializer.Serialize(new { script = Beredskapsskript, args = Array.Empty<string>() });

        while (true)
        {
            var svar = await marionette.KommandoAsync("WebDriver:ExecuteScript", fraga, ct).ConfigureAwait(false);

            if (svar.TryGetProperty("value", out var varde) && varde.ValueKind == JsonValueKind.True)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
        }
    }

    private const string Beredskapsskript = """
        if (document.readyState !== 'complete') { return false; }
        var dagar = ['måndag','tisdag','onsdag','torsdag','fredag','lördag','söndag',
                     'monday','tuesday','wednesday','thursday','friday','saturday','sunday'];
        var alla = document.getElementsByTagName('*');
        for (var i = 0; i < alla.length; i++) {
            var nod = alla[i];
            if (nod.children.length !== 2) { continue; }
            var etikett = nod.children[0];
            if (etikett.children.length === 0) { continue; }
            var text = (etikett.children[0].textContent || '').trim().toLowerCase();
            if (dagar.indexOf(text) >= 0) { return true; }
        }
        return false;
        """;

    private static int LedigPort()
    {
        var lyssnare = new TcpListener(IPAddress.Loopback, 0);
        lyssnare.Start();
        var port = ((IPEndPoint)lyssnare.LocalEndpoint).Port;
        lyssnare.Stop();

        return port;
    }

    private static async Task<Marionette> AnslutAsync(int port, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var klient = new TcpClient();

            try
            {
                await klient.ConnectAsync(IPAddress.Loopback, port, ct).ConfigureAwait(false);

                var kanal = new Marionette(klient);

                await kanal.LasAsync(ct).ConfigureAwait(false);

                return kanal;
            }
            catch (SocketException)
            {
                klient.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(400), ct).ConfigureAwait(false);
            }
        }
    }

    private sealed class Marionette(TcpClient klient) : IDisposable
    {
        private readonly NetworkStream strom = klient.GetStream();
        private readonly List<byte> buffert = [];
        private int nastaId;

        public async Task<JsonElement> KommandoAsync(string kommando, string parametrarJson, CancellationToken ct)
        {
            var id = ++nastaId;
            var kropp = Encoding.UTF8.GetBytes("[0," + id.ToString(CultureInfo.InvariantCulture) + ",\"" + kommando + "\"," + parametrarJson + "]");
            var huvud = Encoding.UTF8.GetBytes(kropp.Length.ToString(CultureInfo.InvariantCulture) + ":");

            await strom.WriteAsync(huvud, ct).ConfigureAwait(false);
            await strom.WriteAsync(kropp, ct).ConfigureAwait(false);

            var svar = await LasAsync(ct).ConfigureAwait(false);

            if (svar.ValueKind != JsonValueKind.Array || svar.GetArrayLength() < 4)
            {
                throw new SchoolMenuFormatException("Marionette svarade något oväntat på " + kommando + ".");
            }

            if (svar[1].TryGetInt32(out var svarsId) && svarsId != id)
            {
                throw new SchoolMenuFormatException("Marionette svarade på fel meddelande (" + svarsId + " i stället för " + id + ").");
            }

            if (svar[2].ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                throw new SchoolMenuFormatException("Marionette nekade " + kommando + ": " + svar[2]);
            }

            return svar[3];
        }

        public async Task<JsonElement> LasAsync(CancellationToken ct)
        {
            var langd = await LasLangdAsync(ct).ConfigureAwait(false);

            while (buffert.Count < langd)
            {
                await FyllPaAsync(ct).ConfigureAwait(false);
            }

            var data = buffert.Take(langd).ToArray();
            buffert.RemoveRange(0, langd);

            return JsonSerializer.Deserialize<JsonElement>(data);
        }

        private async Task<int> LasLangdAsync(CancellationToken ct)
        {
            var siffror = new StringBuilder();

            while (true)
            {
                if (buffert.Count == 0)
                {
                    await FyllPaAsync(ct).ConfigureAwait(false);
                    continue;
                }

                var tecken = (char)buffert[0];
                buffert.RemoveAt(0);

                if (tecken == ':')
                {
                    return int.Parse(siffror.ToString(), CultureInfo.InvariantCulture);
                }

                siffror.Append(tecken);
            }
        }

        private async Task FyllPaAsync(CancellationToken ct)
        {
            var block = new byte[16384];
            var lasta = await strom.ReadAsync(block, ct).ConfigureAwait(false);

            if (lasta == 0)
            {
                throw new SchoolMenuFormatException("Marionette stängde förbindelsen.");
            }

            buffert.AddRange(block.Take(lasta));
        }

        public void Dispose()
        {
            strom.Dispose();
            klient.Dispose();
        }
    }

    private static readonly HtmlParser Html = new();

    private static readonly Dictionary<string, int> Veckodagar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["måndag"] = 0, ["tisdag"] = 1, ["onsdag"] = 2, ["torsdag"] = 3,
        ["fredag"] = 4, ["lördag"] = 5, ["söndag"] = 6,
        ["monday"] = 0, ["tuesday"] = 1, ["wednesday"] = 2, ["thursday"] = 3,
        ["friday"] = 4, ["saturday"] = 5, ["sunday"] = 6,
    };

    private static readonly HashSet<string> InteMat = new(StringComparer.OrdinalIgnoreCase)
    {
        "Idag", "I dag", "Today", "Menyn saknas", "No menu",
    };

    internal static IReadOnlyList<SchoolMenuDay> ParseMenu(string html, int isoAr, int isoVecka)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new SchoolMenuFormatException("Sidan var tom.");
        }

        using var document = Html.ParseDocument(html);
        var mandagen = CalculateIsoWeekMonday(isoAr, isoVecka);
        var dagar = new List<SchoolMenuDay>(7);
        var sedda = new HashSet<DateOnly>();

        foreach (var nod in document.All)
        {
            if (ParseDay(nod, mandagen) is not { } dag || !sedda.Add(dag.Date))
            {
                continue;
            }

            dagar.Add(dag);
        }

        if (dagar.Count == 0)
        {
            throw new SchoolMenuFormatException(
                document.GetElementById("app") is not null
                    ? "Sidan kom fram men innehöll inga dagar. Antingen hann den inte rendera, eller så har skolmaten.se byggt om den."
                    : "Det som renderades är inte skolmaten.se — troligen ett nätverksfel.");
        }

        return [.. dagar.OrderBy(d => d.Date)];
    }

    private static SchoolMenuDay? ParseDay(IElement nod, DateOnly mandagen)
    {
        if (nod.Children.Length != 2)
        {
            return null;
        }

        var etikett = nod.Children[0];

        if (etikett.Children.Length == 0 || !Veckodagar.TryGetValue(Normalisera(etikett.Children[0].TextContent), out var plats))
        {
            return null;
        }

        var datum = mandagen.AddDays(plats);

        KontrolleraDagnummer(Normalisera(etikett.TextContent), datum);

        var ratter = nod.Children[1].Children
            .Select(rad => Normalisera(rad.TextContent))
            .Where(text => text.Length > 0 && !InteMat.Contains(text))
            .ToList();

        return new SchoolMenuDay(datum, ratter);
    }

    internal static string NormalizeSlug(string? inskrivet)
    {
        var text = (inskrivet ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return string.Empty;
        }

        foreach (var kapa in new[] { '?', '#' })
        {
            var vid = text.IndexOf(kapa);

            if (vid >= 0)
            {
                text = text[..vid];
            }
        }

        text = text.TrimEnd('/');
        var sista = text.LastIndexOf('/');

        if (sista >= 0)
        {
            text = text[(sista + 1)..];
        }

        var kvar = text.Where(c => char.IsLetterOrDigit(c) || c == '-');

        return new string([.. kvar]).ToLowerInvariant();
    }

    internal static DateOnly CalculateIsoWeekMonday(int isoAr, int isoVecka) =>
        DateOnly.FromDateTime(ISOWeek.ToDateTime(isoAr, isoVecka, DayOfWeek.Monday));

    private static void KontrolleraDagnummer(string etikett, DateOnly datum)
    {
        var siffror = new string([.. etikett.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit)]);

        if (siffror.Length > 0 && int.TryParse(siffror, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dagnummer) && dagnummer != datum.Day)
        {
            throw new SchoolMenuFormatException($"Sidan säger dag {dagnummer} där {datum:yyyy-MM-dd} väntades. Veckan på sidan är inte den vi bad om.");
        }
    }

    private static string Normalisera(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private string? HittaFirefox()
    {
        var installd = Settings.FirefoxPath;

        if (!string.IsNullOrWhiteSpace(installd))
        {
            return File.Exists(installd) ? installd : null;
        }

        return Kandidater.FirstOrDefault(File.Exists);
    }

    private void Doda(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit(milliseconds: 10_000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            logger.LogDebug(ex, "Firefox hade redan avslutats.");
        }
    }

    private void Stada(string katalog)
    {
        for (var forsok = 1; forsok <= 5; forsok++)
        {
            try
            {
                if (!Directory.Exists(katalog))
                {
                    return;
                }

                Directory.Delete(katalog, recursive: true);

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (forsok == 5)
                {
                    logger.LogWarning(ex, "Kunde inte radera den temporära profilen {Katalog}.", katalog);
                    return;
                }

                Thread.Sleep(millisecondsTimeout: 300);
            }
        }
    }
}

public sealed class SchoolMenuFormatException(string message) : Exception(message);
