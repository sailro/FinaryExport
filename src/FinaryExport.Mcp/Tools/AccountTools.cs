using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using ModelContextProtocol.Server;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class AccountTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_accounts", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get paged active-profile account detail for one category. Account rows are not a totals source; use get_portfolio_summary for totals.")]
	public async Task<AccountsResponse> GetAccounts(
		[Description("Asset category. Options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups"), AllowedValues("checkings", "savings", "investments", "real_estates", "cryptos", "fonds_euro", "commodities", "credits", "other_assets", "startups")] string category,
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period = Defaults.DefaultPeriod,
		[Description("Zero-based account offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum accounts to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		var cat = ParseCategory(category);
		period = ToolInputs.Period(period);
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var accounts = await api.GetCategoryAccountsAsync(cat, period, ct);
		var synchronization = await GetSynchronizationsAsync(api, ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Accounts(
			accounts.Select(account => (cat.ToUrlSegment(), account)),
			period,
			currency,
			offset,
			limit,
			synchronization.Warning is null ? null : [synchronization.Warning],
			synchronization.Items);
	}

	[McpServerTool(Name = "get_all_accounts", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get paged account detail across all categories, deduplicated by account ID. Duplicate category appearances are grouped under category_values. Never sum these rows for a portfolio total.")]
	public async Task<AccountsResponse> GetAllAccounts(
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period = Defaults.DefaultPeriod,
		[Description("Zero-based unique-account offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum unique accounts to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		period = ToolInputs.Period(period);
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var fetches = Enum.GetValues<AssetCategory>().Select(async category =>
		{
			try
			{
				var accounts = await api.GetCategoryAccountsAsync(category, period, ct);
				return (
					Items: accounts.Select(account => (category.ToUrlSegment(), account)).ToList(),
					Warning: (string?)null);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				return (
					Items: new List<(string Category, Account Account)>(),
					Warning: $"{category.ToUrlSegment()}: {ex.Message}");
			}
		});
		var fetched = await Task.WhenAll(fetches);
		var result = fetched.SelectMany(item => item.Items).ToList();
		var synchronization = await GetSynchronizationsAsync(api, ct);
		var warnings = fetched.Select(item => item.Warning)
			.Where(item => item is not null)
			.Select(item => item!);
		if (synchronization.Warning is not null)
			warnings = warnings.Append(synchronization.Warning);

		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Accounts(
			result,
			period,
			currency,
			offset,
			limit,
			warnings,
			synchronization.Items);
	}

	[McpServerTool(Name = "get_category_timeseries", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get a paged historical active-profile value series for one asset category")]
	public async Task<TimeseriesResponse> GetCategoryTimeseries(
		[Description("Asset category. Options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups"), AllowedValues("checkings", "savings", "investments", "real_estates", "cryptos", "fonds_euro", "commodities", "credits", "other_assets", "startups")] string category,
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period = Defaults.DefaultPeriod,
		[Description("Zero-based point offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum points to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		var cat = ParseCategory(category);
		period = ToolInputs.Period(period);
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var series = await api.GetCategoryTimeseriesAsync(cat, period, ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Timeseries(series, period, currency, offset, limit);
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
			_ => throw McpErrors.InvalidInput(
				$"Unknown category '{category}'. Valid options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups")
		};
	}

	private static async Task<(List<AccountSynchronization> Items, string? Warning)> GetSynchronizationsAsync(
		IFinaryApiClient api,
		CancellationToken ct)
	{
		try
		{
			return (await api.GetSynchronizationsAsync(ct), null);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			return ([], $"synchronizations: {ex.Message}");
		}
	}
}
