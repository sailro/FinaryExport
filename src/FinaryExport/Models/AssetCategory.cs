namespace FinaryExport.Models;

public enum AssetCategory
{
	Checkings,
	Savings,
	Investments,
	RealEstates,
	Cryptos,
	FondsEuro,
	Commodities,
	Credits,
	OtherAssets,
	Startups
}

public static class AssetCategoryExtensions
{
	extension(AssetCategory category)
	{
		public string ToUrlSegment() => category switch
		{
			AssetCategory.RealEstates => "real_estates",
			AssetCategory.FondsEuro => "fonds_euro",
			AssetCategory.OtherAssets => "other_assets",
			_ => category.ToString().ToLowerInvariant()
		};

		public string ToDisplayName() => category switch
		{
			AssetCategory.RealEstates => "Real Estate",
			AssetCategory.FondsEuro => "Fonds Euro",
			AssetCategory.OtherAssets => "Other Assets",
			_ => category.ToString()
		};

		public bool HasTransactions() => category switch
		{
			AssetCategory.Checkings => true,
			AssetCategory.Savings => true,
			AssetCategory.Investments => true,
			AssetCategory.Credits => true,
			_ => false
		};
	}

	// Only these 4 categories have /portfolio/{category}/transactions endpoints
}
