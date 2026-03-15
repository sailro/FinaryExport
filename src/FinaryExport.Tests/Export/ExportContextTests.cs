using FinaryExport.Export;
using FluentAssertions;

namespace FinaryExport.Tests.Export;

public sealed class ExportContextTests
{
	[Fact]
	public void ResolveValue_UseDisplayTrue_PrefersDisplayValue()
	{
		var ctx = new ExportContext { UseDisplayValues = true };
		ctx.ResolveValue(displayValue: 500m, rawValue: 1000m).Should().Be(500m);
	}

	[Fact]
	public void ResolveValue_UseDisplayTrue_FallsBackToRaw_WhenDisplayNull()
	{
		var ctx = new ExportContext { UseDisplayValues = true };
		ctx.ResolveValue(displayValue: null, rawValue: 1000m).Should().Be(1000m);
	}

	[Fact]
	public void ResolveValue_UseDisplayFalse_PrefersRawValue()
	{
		var ctx = new ExportContext { UseDisplayValues = false };
		ctx.ResolveValue(displayValue: 500m, rawValue: 1000m).Should().Be(1000m);
	}

	[Fact]
	public void ResolveValue_UseDisplayFalse_FallsBackToDisplay_WhenRawNull()
	{
		var ctx = new ExportContext { UseDisplayValues = false };
		ctx.ResolveValue(displayValue: 500m, rawValue: null).Should().Be(500m);
	}

	[Fact]
	public void ResolveValue_BothNull_ReturnsZero()
	{
		var ctx = new ExportContext { UseDisplayValues = true };
		ctx.ResolveValue(null, null).Should().Be(0m);
	}

	[Fact]
	public void UseDisplayValues_DefaultsToTrue()
	{
		new ExportContext().UseDisplayValues.Should().BeTrue();
	}
}
