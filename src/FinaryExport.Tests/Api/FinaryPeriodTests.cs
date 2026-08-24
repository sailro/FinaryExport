using FinaryExport.Api;
using FluentAssertions;

namespace FinaryExport.Tests.Api;

public sealed class FinaryPeriodTests
{
	[Fact]
	public void GetRange_UsesATrailingCalendarWindow()
	{
		var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

		var range = FinaryPeriod.GetRange("1m", now);

		range.Should().NotBeNull();
		range!.Value.Start.Should().Be(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
		range.Value.End.Should().Be(now);
	}

	[Theory]
	[InlineData("ALL", "all")]
	[InlineData(" 1M ", "1m")]
	[InlineData("5y", "5y")]
	public void Validate_NormalizesKnownPeriods(string input, string expected)
	{
		FinaryPeriod.Validate(input).Should().Be(expected);
	}

	[Fact]
	public void Validate_RejectsUnknownPeriods()
	{
		var act = () => FinaryPeriod.Validate("calendar-month");

		act.Should().Throw<ArgumentException>().WithMessage("*Valid options*");
	}

	[Theory]
	[InlineData(" GROSS ", "gross")]
	[InlineData("Net", "net")]
	public void ValueType_ValidatesAndNormalizesKnownValues(string input, string expected)
	{
		FinaryValueType.Validate(input).Should().Be(expected);
	}

	[Fact]
	public void ValueType_RejectsUnknownValues()
	{
		var act = () => FinaryValueType.Validate("display");

		act.Should().Throw<ArgumentException>().WithMessage("*gross or net*");
	}
}
