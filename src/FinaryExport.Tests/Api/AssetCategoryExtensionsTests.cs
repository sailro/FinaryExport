using FinaryExport.Models;
using FluentAssertions;

namespace FinaryExport.Tests.Api;

public sealed class AssetCategoryExtensionsTests
{
	// ================================================================
	// ToDisplayName
	// ================================================================

	[Theory]
	[InlineData(AssetCategory.Checkings, "Checkings")]
	[InlineData(AssetCategory.Savings, "Savings")]
	[InlineData(AssetCategory.Investments, "Investments")]
	[InlineData(AssetCategory.RealEstates, "Real Estate")]
	[InlineData(AssetCategory.Cryptos, "Cryptos")]
	[InlineData(AssetCategory.FondsEuro, "Fonds Euro")]
	[InlineData(AssetCategory.Commodities, "Commodities")]
	[InlineData(AssetCategory.Credits, "Credits")]
	[InlineData(AssetCategory.OtherAssets, "Other Assets")]
	[InlineData(AssetCategory.Startups, "Startups")]
	public void ToDisplayName_MapsCorrectly(AssetCategory category, string expected)
	{
		category.ToDisplayName().Should().Be(expected);
	}

	// ================================================================
	// HasTransactions
	// ================================================================

	[Theory]
	[InlineData(AssetCategory.Checkings, true)]
	[InlineData(AssetCategory.Savings, true)]
	[InlineData(AssetCategory.Investments, true)]
	[InlineData(AssetCategory.Credits, true)]
	[InlineData(AssetCategory.RealEstates, false)]
	[InlineData(AssetCategory.Cryptos, false)]
	[InlineData(AssetCategory.FondsEuro, false)]
	[InlineData(AssetCategory.Commodities, false)]
	[InlineData(AssetCategory.OtherAssets, false)]
	[InlineData(AssetCategory.Startups, false)]
	public void HasTransactions_ReturnsTrueOnlyForSupportedCategories(AssetCategory category, bool expected)
	{
		category.HasTransactions().Should().Be(expected);
	}

	[Fact]
	public void HasTransactions_ExactlyFourCategoriesAreTrue()
	{
		var transactionCapable = Enum.GetValues<AssetCategory>()
			.Where(c => c.HasTransactions())
			.ToList();

		transactionCapable.Should().HaveCount(4, "only 4 categories support transactions");
		transactionCapable.Should().BeEquivalentTo(
			[AssetCategory.Checkings, AssetCategory.Savings, AssetCategory.Investments, AssetCategory.Credits]);
	}

	// ================================================================
	// ToUrlSegment (additional coverage beyond existing tests)
	// ================================================================

	[Fact]
	public void ToUrlSegment_DefaultCase_UsesLowercaseInvariant()
	{
		// Categories using the default path should be lowercase of enum name
		AssetCategory.Checkings.ToUrlSegment().Should().Be("checkings");
		AssetCategory.Commodities.ToUrlSegment().Should().Be("commodities");
	}

	[Fact]
	public void ToUrlSegment_SpecialCases_UseSnakeCase()
	{
		AssetCategory.RealEstates.ToUrlSegment().Should().Be("real_estates");
		AssetCategory.FondsEuro.ToUrlSegment().Should().Be("fonds_euro");
		AssetCategory.OtherAssets.ToUrlSegment().Should().Be("other_assets");
	}

	[Fact]
	public void AllCategories_HaveValidUrlSegment()
	{
		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			var segment = cat.ToUrlSegment();
			segment.Should().NotBeNullOrEmpty($"{cat} should have a valid URL segment");
			segment.Should().MatchRegex("^[a-z_]+$", $"{cat} URL segment should be lowercase with underscores");
		}
	}
}
