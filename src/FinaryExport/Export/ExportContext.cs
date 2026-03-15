namespace FinaryExport.Export;

// Controls whether sheet writers use ownership-adjusted (display) or raw values.
// Per-profile exports use display values; the unified export uses raw totals.
public sealed record ExportContext
{
	public bool UseDisplayValues { get; init; } = true;

	// Picks the display or raw value depending on context.
	// Falls back to the other when the preferred one is null.
	public decimal ResolveValue(decimal? displayValue, decimal? rawValue)
	=> (UseDisplayValues ? displayValue ?? rawValue : rawValue ?? displayValue) ?? 0m;
}
