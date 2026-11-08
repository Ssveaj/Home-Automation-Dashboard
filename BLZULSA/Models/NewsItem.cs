using System.Globalization;

namespace BLZULSA.Models;
public sealed record NewsItem(string Title, string Summary, string? Link, DateTimeOffset? Published)
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");
    public string When
    {
        get
        {
            if (Published is not { } at)
            {
                return string.Empty;
            }

            var local = at.ToLocalTime().DateTime;
            var today = DateTime.Now.Date;
            var day = local.Date;

            if (day == today)
            {
                return local.ToString("HH:mm", Swedish);
            }

            return day == today.AddDays(-1) ? $"Igår {local.ToString("HH:mm", Swedish)}" : local.ToString("d MMM", Swedish);
        }
    }
}
