namespace FinaryExport.Models.Accounts;

public sealed record SecurityPosition
{
	public decimal? Quantity { get; init; }
	public decimal? BuyingPrice { get; init; }
	public decimal? DisplayBuyingPrice { get; init; }
	public decimal? BuyingValue { get; init; }
	public decimal? DisplayBuyingValue { get; init; }
	public decimal? CurrentValue { get; init; }
	public decimal? DisplayCurrentValue { get; init; }
	public decimal? CurrentUpnl { get; init; }
	public decimal? DisplayCurrentUpnl { get; init; }
	public decimal? CurrentUpnlPercent { get; init; }
	public decimal? DisplayCurrentUpnlPercent { get; init; }
	public decimal? UnrealizedPnl { get; init; }
	public decimal? UnrealizedPnlPercent { get; init; }
	public long? Id { get; init; }
	public string? Type { get; init; }
	public SecurityInfo? Security { get; init; }
}
