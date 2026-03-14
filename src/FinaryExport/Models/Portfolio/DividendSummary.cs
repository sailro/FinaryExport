using System.Text.Json;

namespace FinaryExport.Models.Portfolio;

public sealed record DividendSummary
{
    public decimal? AnnualIncome { get; init; }
    public decimal? PastIncome { get; init; }

    // API returns an array of { date, value } objects
    public List<NextYearEntry>? NextYear { get; init; }

    public decimal? Yield { get; init; }
    public List<DividendEntry>? PastDividends { get; init; }
    public List<DividendEntry>? UpcomingDividends { get; init; }

    // Per-asset-type breakdowns
    public decimal? PastIncomeRealEstate { get; init; }
    public decimal? AnnualIncomeRealEstate { get; init; }
    public decimal? PastIncomeEtf { get; init; }
    public decimal? AnnualIncomeEtf { get; init; }
    public decimal? PastIncomeFund { get; init; }
    public decimal? AnnualIncomeFund { get; init; }
    public decimal? PastIncomeEquity { get; init; }
    public decimal? AnnualIncomeEquity { get; init; }
    public decimal? PastIncomeScpi { get; init; }
    public decimal? AnnualIncomeScpi { get; init; }
    public decimal? YieldRealEstate { get; init; }
    public decimal? YieldEtf { get; init; }
    public decimal? YieldFund { get; init; }
    public decimal? YieldEquity { get; init; }
    public decimal? YieldScpi { get; init; }

    // Per-type next_year/past_dividends/upcoming_dividends left as JsonElement for flexibility
    public JsonElement? NextYearRealEstate { get; init; }
    public JsonElement? NextYearEtf { get; init; }
    public JsonElement? NextYearFund { get; init; }
    public JsonElement? NextYearEquity { get; init; }
    public JsonElement? NextYearScpi { get; init; }
    public JsonElement? PastDividendsRealEstate { get; init; }
    public JsonElement? PastDividendsEtf { get; init; }
    public JsonElement? PastDividendsFund { get; init; }
    public JsonElement? PastDividendsEquity { get; init; }
    public JsonElement? PastDividendsScpi { get; init; }
    public JsonElement? UpcomingDividendsRealEstate { get; init; }
    public JsonElement? UpcomingDividendsEtf { get; init; }
    public JsonElement? UpcomingDividendsFund { get; init; }
    public JsonElement? UpcomingDividendsEquity { get; init; }
    public JsonElement? UpcomingDividendsScpi { get; init; }
}

public sealed record NextYearEntry
{
    public string? Date { get; init; }
    public decimal? Value { get; init; }
}

public sealed record DividendEntry
{
    public decimal? Amount { get; init; }
    public decimal? DisplayAmount { get; init; }
    public int? Id { get; init; }
    public string? ExDividendAt { get; init; }
    public string? PaymentAt { get; init; }
    public string? ReceivedAt { get; init; }
    public bool? Hidden { get; init; }
    public string? AssetType { get; init; }
    public string? AssetSubtype { get; init; }
    public string? Status { get; init; }

    // Currency left as JsonElement for flexibility
    public JsonElement? Currency { get; init; }
    public JsonElement? DisplayCurrency { get; init; }
    
    // Typed asset/holding info
    public DividendAssetInfo? Asset { get; init; }
    public DividendAssetInfo? Holding { get; init; }
}

public sealed record DividendAssetInfo
{
    public int? Id { get; init; }
    public string? Type { get; init; }
    public string? Name { get; init; }
    public string? CorrelationId { get; init; }
    public string? LogoUrl { get; init; }
}
