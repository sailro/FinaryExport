using System.Globalization;

namespace FinaryExport.Export;

/// <summary>
/// Converts period strings to cutoff dates for client-side filtering.
/// The Finary API ignores the period query parameter on several endpoints, so we filter locally.
/// </summary>
public static class PeriodHelper
{
	/// <summary>
	/// Returns the UTC cutoff date for the given period, or null when no filtering is needed.
	/// </summary>
	public static DateTime? GetCutoffDate(string? period)
	{
		var now = DateTime.UtcNow;

		return period?.ToLowerInvariant() switch
		{
			"1d" => now.AddDays(-1),
			"1w" => now.AddDays(-7),
			"1m" => now.AddMonths(-1),
			"3m" => now.AddMonths(-3),
			"6m" => now.AddMonths(-6),
			"1y" => now.AddYears(-1),
			"ytd" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			"all" => null,
			null => null,
			"" => null,
			_ => null,
		};
	}

	/// <summary>
	/// Returns true if dateString is on or after the cutoff.
	/// Returns true (include) when the date cannot be parsed - never silently drop data.
	/// </summary>
	public static bool IsOnOrAfter(string? dateString, DateTime? cutoff)
	{
		if (cutoff is null)
			return true;

		if (string.IsNullOrWhiteSpace(dateString))
			return true;

		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
			return date >= cutoff.Value;

		return true;
	}
}
