using System.Text.Json;

namespace FinaryExport.Models.Portfolio;

public sealed record PortfolioSummary
{
    public DateTimeOffset? CreatedAt { get; init; }
    public PortfolioValues? Gross { get; init; }
    public PortfolioValues? Net { get; init; }
    public PortfolioValues? Finary { get; init; }
    public bool HasUnqualifiedLoans { get; init; }
    public bool HasUnlinkedLoans { get; init; }
}

// Each of gross/net/finary contains a total summary object plus asset/liability breakdowns
public sealed record PortfolioValues
{
    public PortfolioTotalValues? Total { get; init; }
    public JsonElement? Assets { get; init; }
    public JsonElement? Liabilities { get; init; }
}

// The "total" object inside each portfolio section
public sealed record PortfolioTotalValues
{
    public decimal? Amount { get; init; }
    public decimal? DisplayAmount { get; init; }
    public decimal? Evolution { get; init; }
    public decimal? PeriodEvolution { get; init; }
    public decimal? DisplayUpnlDifference { get; init; }
    public decimal? DisplayValueDifference { get; init; }
    public decimal? Share { get; init; }
    public decimal? EvolutionPercent { get; init; }
    public decimal? PeriodEvolutionPercent { get; init; }
    public decimal? DisplayUpnlPercent { get; init; }
    public decimal? DisplayValueEvolution { get; init; }
}
