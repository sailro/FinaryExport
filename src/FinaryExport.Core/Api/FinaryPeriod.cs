using System.Globalization;

namespace FinaryExport.Api;

public static class FinaryPeriod
{
	private static readonly HashSet<string> ValidPeriods =
		["all", "1d", "1w", "1m", "3m", "6m", "1y", "5y"];

	public static string Validate(string period)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(period);
		var normalized = period.Trim().ToLowerInvariant();
		if (!ValidPeriods.Contains(normalized))
			throw new ArgumentException(
				$"Unknown period '{period}'. Valid options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y",
				nameof(period));

		return normalized;
	}

	public static (DateTimeOffset Start, DateTimeOffset End)? GetRange(
		string period,
		DateTimeOffset? now = null)
	{
		var normalized = Validate(period);
		if (normalized == "all") return null;

		var end = now ?? DateTimeOffset.UtcNow;
		var start = normalized switch
		{
			"1d" => end.AddDays(-1),
			"1w" => end.AddDays(-7),
			"1m" => end.AddMonths(-1),
			"3m" => end.AddMonths(-3),
			"6m" => end.AddMonths(-6),
			"1y" => end.AddYears(-1),
			"5y" => end.AddYears(-5),
			_ => throw new System.Diagnostics.UnreachableException()
		};

		return (start, end);
	}

	public static bool Contains(
		string? transactionDate,
		(DateTimeOffset Start, DateTimeOffset End)? range)
	{
		if (range is null) return true;
		if (!DateTimeOffset.TryParse(
			transactionDate,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal,
			out var parsed))
			return false;

		return parsed >= range.Value.Start && parsed <= range.Value.End;
	}
}
