namespace BLZULSA.Models;

public sealed record FuelCounty(string Slug, string Name)
{
    public string Url => $"https://www.henrikhjelm.se/api/getdata.php?lan={Slug}";
}
public static class FuelCounties
{
    public static readonly IReadOnlyList<FuelCounty> All =
    [
        .. new FuelCounty[]
        {
            new("blekinge-lan", "Blekinge"), new("dalarnas-lan", "Dalarna"),
            new("gotlands-lan", "Gotland"), new("gavleborgs-lan", "Gävleborg"),
            new("hallands-lan", "Halland"), new("jamtlands-lan", "Jämtland"),
            new("jonkoping-lan", "Jönköping"), new("kalmar-lan", "Kalmar"),
            new("kronobergs-lan", "Kronoberg"), new("norrbottens-lan", "Norrbotten"),
            new("skane-lan", "Skåne"), new("stockholms-lan", "Stockholm"),
            new("sodermanlands-lan", "Södermanland"), new("uppsala-lan", "Uppsala"),
            new("varmlands-lan", "Värmland"), new("vasterbottens-lan", "Västerbotten"),
            new("vasternorrlands-lan", "Västernorrland"), new("vastmanlands-lan", "Västmanland"),
            new("vastra-gotalands-lan", "Västra Götaland"), new("orebro-lan", "Örebro"),
            new("ostergotlands-lan", "Östergötland"),
        }.OrderBy(c => c.Name, StringComparer.CurrentCulture),
    ];
    public static FuelCounty? FromUrl(string? url) => string.IsNullOrWhiteSpace(url) ? null : All.FirstOrDefault(c => url.Contains($"lan={c.Slug}", StringComparison.OrdinalIgnoreCase));
}
