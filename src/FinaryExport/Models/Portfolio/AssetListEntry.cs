namespace FinaryExport.Models.Portfolio;

// Individual holding/position returned by the /asset_list endpoint.
public sealed record AssetListEntry
{
	public string? Name { get; init; }
	public string? Symbol { get; init; }
	public string? AssetType { get; init; }
	public string? CategoryName { get; init; }
	public string? AccountId { get; init; }
	public long? AssetId { get; init; }
	public long? HoldingId { get; init; }
	public string? HoldingType { get; init; }
	public long? ValuableId { get; init; }
	public string? ValuableType { get; init; }
	public decimal? DisplayCurrentValue { get; init; }
	public decimal? DisplayUpnlDifference { get; init; }
	public decimal? DisplayUpnlPercent { get; init; }
	public decimal? CurrentValue { get; init; }
	public decimal? Evolution { get; init; }
	public decimal? EvolutionPercent { get; init; }
	public decimal? UnrealizedPnl { get; init; }
	public decimal? UnrealizedPnlPercent { get; init; }
	public string? LogoUrl { get; init; }
}
