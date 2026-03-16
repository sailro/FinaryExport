using System.Text.Json;

namespace FinaryExport.Models.Portfolio;

public sealed record FeeSummary
{
	public FeeTotalValues? Total { get; init; }

	// API returns array of [date, savings_amount, fees_amount] tuples
	public JsonElement? Timeseries { get; init; }

	// Complex nested structure with asset-level fee details
	public JsonElement? Data { get; init; }
}

public sealed record FeeTotalValues
{
	public decimal? AnnualFeesAmount { get; init; }
	public decimal? AnnualSavingsAmount { get; init; }
	public decimal? CumulatedFeesAmount { get; init; }
	public decimal? CumulatedSavingsAmount { get; init; }
	public decimal? AnnualPotentialSavingsAmount { get; init; }
	public decimal? CumulatedPotentialSavingsAmount { get; init; }
	public decimal? AnnualFeesPercent { get; init; }
	public decimal? AnnualSavingsPercent { get; init; }
	public decimal? CumulatedFeesPercent { get; init; }
	public decimal? CumulatedSavingsPercent { get; init; }
	public decimal? AnnualPotentialSavingsPercent { get; init; }
	public decimal? CumulatedPotentialSavingsPercent { get; init; }
}
