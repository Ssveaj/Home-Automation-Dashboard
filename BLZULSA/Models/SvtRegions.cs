namespace BLZULSA.Models;

public sealed record SvtRegion(string Slug, string Name)
{
    public string Url => $"https://www.svt.se/nyheter/lokalt/{Slug}/rss.xml";
    public string Title => $"SVT Nyheter {Name}";
}
public static class SvtRegions
{
    public static readonly IReadOnlyList<SvtRegion> All =
    [
        .. new SvtRegion[]
        {
            new("blekinge", "Blekinge"), new("dalarna", "Dalarna"), new("gavleborg", "Gävleborg"),
            new("halland", "Halland"), new("helsingborg", "Helsingborg"), new("jamtland", "Jämtland"),
            new("jonkoping", "Jönköping"), new("norrbotten", "Norrbotten"), new("skane", "Skåne"),
            new("smaland", "Småland"), new("stockholm", "Stockholm"), new("sodertalje", "Södertälje"),
            new("sormland", "Sörmland"), new("uppsala", "Uppsala"), new("varmland", "Värmland"),
            new("vast", "Väst"), new("vasterbotten", "Västerbotten"), new("vasternorrland", "Västernorrland"),
            new("vastmanland", "Västmanland"), new("orebro", "Örebro"), new("ost", "Öst"),
        }.OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];
    public static SvtRegion? FromUrl(string? url) => All.FirstOrDefault(r => string.Equals(r.Url, url?.Trim(), StringComparison.OrdinalIgnoreCase));
}
