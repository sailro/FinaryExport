using FluentAssertions;
using FinaryExport.Export;

namespace FinaryExport.Tests.Export;

public sealed class PeriodHelperTests
{
	[Theory]
	[InlineData("all")]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("unknown_period")]
	public void GetCutoffDate_ReturnsNull_ForNoFilterPeriods(string? period)
	{
		PeriodHelper.GetCutoffDate(period).Should().BeNull();
	}

	[Fact]
	public void GetCutoffDate_1d_ReturnsApproximatelyOneDayAgo()
	{
		var cutoff = PeriodHelper.GetCutoffDate("1d");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetCutoffDate_1w_ReturnsApproximatelyOneWeekAgo()
	{
		var cutoff = PeriodHelper.GetCutoffDate("1w");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(-7), TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetCutoffDate_1m_ReturnsApproximatelyOneMonthAgo()
	{
		var cutoff = PeriodHelper.GetCutoffDate("1m");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-1), TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetCutoffDate_3m_ReturnsApproximatelyThreeMonthsAgo()
	{
		var cutoff = PeriodHelper.GetCutoffDate("3m");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-3), TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetCutoffDate_6m_ReturnsApproximatelySixMonthsAgo()
	{
		var cutoff = PeriodHelper.GetCutoffDate("6m");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-6), TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetCutoffDate_1y_ReturnsApproximatelyOneYearAgo()
	{
		var cutoff = PeriodHelper.GetCutoffDate("1y");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddYears(-1), TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetCutoffDate_Ytd_ReturnsJanuaryFirst()
	{
		var cutoff = PeriodHelper.GetCutoffDate("ytd");
		cutoff.Should().NotBeNull();
		cutoff!.Value.Should().Be(new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void GetCutoffDate_IsCaseInsensitive()
	{
		PeriodHelper.GetCutoffDate("YTD").Should().NotBeNull();
		PeriodHelper.GetCutoffDate("1M").Should().NotBeNull();
		PeriodHelper.GetCutoffDate("ALL").Should().BeNull();
	}

	[Fact]
	public void IsOnOrAfter_NullCutoff_AlwaysReturnsTrue()
	{
		PeriodHelper.IsOnOrAfter("2020-01-01", null).Should().BeTrue();
		PeriodHelper.IsOnOrAfter(null, null).Should().BeTrue();
	}

	[Fact]
	public void IsOnOrAfter_DateAfterCutoff_ReturnsTrue()
	{
		var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		PeriodHelper.IsOnOrAfter("2024-07-15", cutoff).Should().BeTrue();
	}

	[Fact]
	public void IsOnOrAfter_DateOnCutoff_ReturnsTrue()
	{
		var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		PeriodHelper.IsOnOrAfter("2024-06-01", cutoff).Should().BeTrue();
	}

	[Fact]
	public void IsOnOrAfter_DateBeforeCutoff_ReturnsFalse()
	{
		var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		PeriodHelper.IsOnOrAfter("2024-05-31", cutoff).Should().BeFalse();
	}

	[Fact]
	public void IsOnOrAfter_NullDate_ReturnsTrue_NeverDropsData()
	{
		var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		PeriodHelper.IsOnOrAfter(null, cutoff).Should().BeTrue();
	}

	[Fact]
	public void IsOnOrAfter_EmptyDate_ReturnsTrue_NeverDropsData()
	{
		var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		PeriodHelper.IsOnOrAfter("", cutoff).Should().BeTrue();
	}

	[Fact]
	public void IsOnOrAfter_UnparseableDate_ReturnsTrue_NeverDropsData()
	{
		var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		PeriodHelper.IsOnOrAfter("not-a-date", cutoff).Should().BeTrue();
	}
}
