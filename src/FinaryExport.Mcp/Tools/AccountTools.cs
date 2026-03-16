using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class AccountTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_accounts"), Description("Get all accounts for a specific asset category with balances, ownership, and institution details")]
	public async Task<List<Account>> GetAccounts(
		[Description("Asset category. Options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups")] string category,
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = "all",
		CancellationToken ct = default)
	{
		var cat = ParseCategory(category);
		return await api.GetCategoryAccountsAsync(cat, period, ct);
	}

	[McpServerTool(Name = "get_all_accounts"), Description("Get accounts across ALL asset categories in a single call, grouped by category name")]
	public async Task<Dictionary<string, List<Account>>> GetAllAccounts(
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = "all",
		CancellationToken ct = default)
	{
		var result = new Dictionary<string, List<Account>>();

		foreach (var category in Enum.GetValues<AssetCategory>())
		{
			try
			{
				var accounts = await api.GetCategoryAccountsAsync(category, period, ct);
				if (accounts.Count > 0)
					result[category.ToUrlSegment()] = accounts;
			}
			catch
			{
				// Skip categories that fail — return what we can
			}
		}

		return result;
	}

	[McpServerTool(Name = "get_category_timeseries"), Description("Get historical value timeseries for a specific asset category")]
	public async Task<List<TimeseriesData>> GetCategoryTimeseries(
		[Description("Asset category. Options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups")] string category,
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = "all",
		CancellationToken ct = default)
	{
		var cat = ParseCategory(category);
		return await api.GetCategoryTimeseriesAsync(cat, period, ct);
	}

	internal static AssetCategory ParseCategory(string category)
	{
		return category.ToLowerInvariant().Trim() switch
		{
			"checkings" => AssetCategory.Checkings,
			"savings" => AssetCategory.Savings,
			"investments" => AssetCategory.Investments,
			"real_estates" => AssetCategory.RealEstates,
			"cryptos" => AssetCategory.Cryptos,
			"fonds_euro" => AssetCategory.FondsEuro,
			"commodities" => AssetCategory.Commodities,
			"credits" => AssetCategory.Credits,
			"other_assets" => AssetCategory.OtherAssets,
			"startups" => AssetCategory.Startups,
			_ => throw new ArgumentException(
				$"Unknown category '{category}'. Valid options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups")
		};
	}
}
