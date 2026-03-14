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
    public static string ToUrlSegment(this AssetCategory category) => category switch
    {
        AssetCategory.RealEstates => "real_estates",
        AssetCategory.FondsEuro => "fonds_euro",
        AssetCategory.OtherAssets => "other_assets",
        _ => category.ToString().ToLowerInvariant()
    };

    public static string ToDisplayName(this AssetCategory category) => category switch
    {
        AssetCategory.RealEstates => "Real Estate",
        AssetCategory.FondsEuro => "Fonds Euro",
        AssetCategory.OtherAssets => "Other Assets",
        _ => category.ToString()
    };

    // Only these 4 categories have /portfolio/{category}/transactions endpoints
    public static bool HasTransactions(this AssetCategory category) => category switch
    {
        AssetCategory.Checkings => true,
        AssetCategory.Savings => true,
        AssetCategory.Investments => true,
        AssetCategory.Credits => true,
        _ => false
    };
}
