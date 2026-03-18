using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class HoldingsTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_holdings"), Description("Get all holdings accounts with their security positions, balances, and transaction details")]
	public async Task<List<HoldingsAccount>> GetHoldings(CancellationToken ct = default)
	{
		return await api.GetHoldingsAccountsAsync(ct);
	}

	[McpServerTool(Name = "get_account_positions"), Description("Get positions within a specific account. Returns securities (stocks, ETFs, funds) for investment accounts, crypto positions for crypto accounts, or fiat positions for checking/savings accounts. Use get_accounts first to find the account ID.")]
	public async Task<object> GetAccountPositions(
		[Description("The account ID (from get_accounts response)")] string account_id,
		[Description("Asset category. Options: checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups. Default: investments")] string category = "investments",
		CancellationToken ct = default)
	{
		var cat = AccountTools.ParseCategory(category);
		var accounts = await api.GetCategoryAccountsAsync(cat, "all", ct);

		var account = accounts.FirstOrDefault(a =>
			string.Equals(a.Id, account_id, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(a.Slug, account_id, StringComparison.OrdinalIgnoreCase));

		if (account is null)
		{
			return new { error = $"Account '{account_id}' not found in category '{category}'. Use get_accounts to list available accounts and their IDs." };
		}

		// Return the appropriate position list based on category
		object positions = cat switch
		{
			AssetCategory.Cryptos => account.Cryptos ?? [],
			AssetCategory.Checkings or AssetCategory.Savings => account.Fiats ?? [],
			_ => account.Securities ?? []
		};

		return new
		{
			account_id = account.Id,
			account_name = account.Name,
			institution = account.Institution?.Name,
			positions
		};
	}

	[McpServerTool(Name = "get_crypto_holdings"), Description("Get a complete overview of all crypto holdings across every crypto account. Returns each position with coin name, quantity, current value, buying cost, and unrealized P&L. Includes a total portfolio value. Use this for questions about crypto portfolio performance or specific coin holdings.")]
	public async Task<object> GetCryptoHoldings(CancellationToken ct = default)
	{
		var accounts = await api.GetCategoryAccountsAsync(AssetCategory.Cryptos, ct: ct);

		var allCurrencyPositions = accounts
			.SelectMany(a => (a.Cryptos ?? []).Concat(a.Fiats ?? []))
			.ToList();

		var totalValue = allCurrencyPositions.Sum(p => p.DisplayCurrentValue ?? p.CurrentValue ?? 0m);

		var positions = accounts
			.SelectMany(account =>
			{
				var cryptos = (account.Cryptos ?? []).Select(p => MapCurrencyPosition(account.Name, p));
				var fiats = (account.Fiats ?? []).Select(p => MapCurrencyPosition(account.Name, p));
				return cryptos.Concat(fiats);
			})
			.ToList();

		return new
		{
			positions,
			total_value = totalValue,
			account_count = accounts.Count,
			position_count = positions.Count
		};
	}

	private static object MapCurrencyPosition(string? accountName, CurrencyPosition p)
	{
		var asset = p.Asset;
		return new
		{
			account_name = accountName,
			name = asset?.Name,
			code = asset?.Code,
			logo_url = asset?.LogoUrl,
			quantity = p.Quantity,
			current_price = p.DisplayCurrentPrice ?? p.CurrentPrice,
			current_value = p.DisplayCurrentValue ?? p.CurrentValue,
			buying_price = p.DisplayBuyingPrice ?? p.BuyingPrice,
			buying_value = p.DisplayBuyingValue ?? p.BuyingValue,
			unrealized_pnl = p.DisplayUnrealizedPnl ?? p.UnrealizedPnl,
			unrealized_pnl_percent = p.UnrealizedPnlPercent
		};
	}
}
