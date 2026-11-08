namespace BLZULSA.Models;

public sealed record StockQuote(string Symbol, string Label, decimal? Price, decimal? ChangePercent)
{
    public string? Exchange => StockExchanges.Describe(Symbol);
}
public sealed record StockSuggestion(string Symbol, string Description, string? Type)
{
    public string Label => string.IsNullOrWhiteSpace(Description) ? Symbol : Description;
    public string? Exchange => StockExchanges.Describe(Symbol);
}
public static class StockExchanges
{
    private static readonly Dictionary<string, string> BySuffix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ST"] = "Stockholm",
        ["HE"] = "Helsingfors",
        ["OL"] = "Oslo",
        ["CO"] = "Köpenhamn",
        ["DE"] = "Frankfurt",
        ["F"] = "Frankfurt",
        ["L"] = "London",
        ["PA"] = "Paris",
        ["AS"] = "Amsterdam",
        ["MI"] = "Milano",
        ["SW"] = "Zürich",
        ["TO"] = "Toronto",
        ["HK"] = "Hongkong",
        ["T"] = "Tokyo",
        ["AX"] = "Sydney",
    };
    public static string? Describe(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var dot = symbol.LastIndexOf('.');

        if (dot < 0 || dot == symbol.Length - 1)
        {
            return null;
        }

        var suffix = symbol[(dot + 1)..];

        return BySuffix.TryGetValue(suffix, out var name) ? name : suffix.ToUpperInvariant();
    }
}
