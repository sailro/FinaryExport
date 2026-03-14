namespace FinaryExport.Models.Accounts;

public sealed record SecurityInfo
{
    public string? Name { get; init; }
    public string? Isin { get; init; }
    public string? Symbol { get; init; }
    public string? SecurityType { get; init; }
    public decimal? CurrentPrice { get; init; }
    public decimal? DisplayCurrentPrice { get; init; }
    public decimal? ExpenseRatio { get; init; }
    public AccountCurrency? Currency { get; init; }
}
