using System.Text.Json;

namespace FinaryExport.Models.Portfolio;

public sealed record AllocationData
{
	public decimal? Total { get; init; }
	public decimal? Share { get; init; }
	public List<AllocationEntry>? Distribution { get; init; }
}

public sealed record AllocationEntry
{
	public decimal? Amount { get; init; }
	public string? Label { get; init; }
	public decimal? Share { get; init; }

	// Nested sub-distributions (sectors have sub-sectors, etc.)
	public List<AllocationEntry>? Distribution { get; init; }

	// Sub-entries may include contribution details
	public JsonElement? Contributions { get; init; }
}
