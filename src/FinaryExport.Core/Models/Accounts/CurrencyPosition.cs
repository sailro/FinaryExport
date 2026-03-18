using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinaryExport.Models.Accounts;

public sealed record CurrencyPosition
{
	// API sends long for crypto, string for fiat — JsonElement handles both
	public JsonElement? Id { get; init; }
	public string? CorrelationId { get; init; }
	public string? Type { get; init; }
	public string? OwningType { get; init; }
	public decimal? Quantity { get; init; }
	public decimal? BuyingPrice { get; init; }
	public decimal? DisplayBuyingPrice { get; init; }
	public decimal? BuyingValue { get; init; }
	public decimal? DisplayBuyingValue { get; init; }
	public decimal? CurrentPrice { get; init; }
	public decimal? DisplayCurrentPrice { get; init; }
	public decimal? CurrentValue { get; init; }
	public decimal? DisplayCurrentValue { get; init; }
	public decimal? UnrealizedPnl { get; init; }
	public decimal? DisplayUnrealizedPnl { get; init; }
	public decimal? UnrealizedPnlPercent { get; init; }

	// JSON maps "crypto" or "fiat" — only one is populated per position
	public AssetInfo? Crypto { get; init; }
	public AssetInfo? Fiat { get; init; }

	// Consumer convenience — use this instead of checking Crypto/Fiat
	[JsonIgnore]
	public AssetInfo? Asset => Crypto ?? Fiat;
}
