namespace BLZULSA.Models;
public sealed record InterestRate(string SeriesId, string Label, decimal Percent, DateOnly Observed);
