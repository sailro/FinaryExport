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

	[McpServerTool(Name = "get_account_positions"), Description("Get the individual securities/positions within a specific investment account. Returns the holdings (stocks, ETFs, funds) for one account by ID.")]
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

		return new
		{
			account_id = account.Id,
			account_name = account.Name,
			institution = account.Institution?.Name,
			positions = account.Securities ?? []
		};
	}
}
