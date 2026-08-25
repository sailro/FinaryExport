using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class HoldingsTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_holdings", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get active-profile investment holdings with security, fiat, SCPI, and fonds-euro positions. This tool does not return transactions.")]
	public async Task<HoldingsResponse> GetHoldings(
		[Description("Zero-based position offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum positions to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var components = await GetInvestmentComponentsAsync(api, ct);
		var mapped = components
			.GroupBy(component => AccountKey(component.Category, component.Account), StringComparer.OrdinalIgnoreCase)
			.Select(group => new
			{
				Components = group.ToList(),
				Account = McpMapper.InvestmentAccount(group)
			})
			.Where(item =>
				item.Components.Any(component => component.Category is AssetCategory.Investments or AssetCategory.FondsEuro) ||
				item.Account.Positions.Any(position => position.Kind == "scpi"))
			.Select(item => item.Account)
			.ToList();
		var page = PagePositions(mapped, offset, limit);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return new HoldingsResponse
		{
			DisplayCurrency = currency,
			AccountCount = mapped.Count,
			PositionCount = page.Total,
			ReturnedPositionCount = page.Returned,
			Offset = offset,
			Limit = limit,
			HasMore = page.HasMore,
			Accounts = page.Accounts
		};
	}

	[McpServerTool(Name = "get_account_positions", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get typed positions inside one active-profile investment, real-estate, fonds-euro, crypto, checking, or savings account. Use get_accounts to find the account ID.")]
	public async Task<AccountPositionsResponse> GetAccountPositions(
		[Description("The account ID (from get_accounts response)")] string account_id,
		[Description("Asset category. Options: checkings, savings, investments, real_estates, fonds_euro, cryptos. Default: investments"), AllowedValues("checkings", "savings", "investments", "real_estates", "fonds_euro", "cryptos")] string category = "investments",
		[Description("Zero-based position offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum positions to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(account_id))
			throw McpErrors.InvalidInput("Account ID is required.");
		(offset, limit) = ToolInputs.Page(offset, limit);
		var cat = AccountTools.ParseCategory(category);
		if (cat is not (AssetCategory.Investments or AssetCategory.RealEstates or AssetCategory.FondsEuro or AssetCategory.Cryptos or AssetCategory.Checkings or AssetCategory.Savings))
			throw McpErrors.InvalidInput(
				$"Position details are not modeled for category '{category}'. Supported categories: investments, real_estates, fonds_euro, cryptos, checkings, savings.");

		var api = await clients.CreateActiveClientAsync(ct);
		var investmentLike = cat is AssetCategory.Investments or AssetCategory.RealEstates or AssetCategory.FondsEuro;
		var components = investmentLike
			? await GetInvestmentComponentsAsync(api, ct)
			: [];
		var accounts = investmentLike
			? components.Where(component => component.Category == cat).Select(component => component.Account).ToList()
			: await api.GetCategoryAccountsAsync(cat, "all", ct);

		var account = accounts.FirstOrDefault(a =>
			string.Equals(a.Id, account_id, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(a.Slug, account_id, StringComparison.OrdinalIgnoreCase));

		if (account is null)
			throw McpErrors.NotFound(
				$"Account '{account_id}' was not found in category '{category}'. Use get_accounts to list available account IDs.");

		var mapped = investmentLike
			? McpMapper.InvestmentAccount(RelatedComponents(components, cat, account))
			: McpMapper.CurrencyAccount(account);
		var total = mapped.Positions.Count;
		var positions = mapped.Positions.Skip(offset).Take(limit).ToList();
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return new AccountPositionsResponse
		{
			DisplayCurrency = currency,
			PositionCount = total,
			ReturnedPositionCount = positions.Count,
			Offset = offset,
			Limit = limit,
			HasMore = (long)offset + limit < total,
			Account = mapped with { Positions = positions }
		};
	}

	private static async Task<List<(AssetCategory Category, Account Account)>> GetInvestmentComponentsAsync(
		IFinaryApiClient api,
		CancellationToken ct)
	{
		var categories = new[]
		{
			AssetCategory.Investments,
			AssetCategory.RealEstates,
			AssetCategory.FondsEuro
		};
		var fetches = categories.Select(async category =>
		{
			var accounts = await api.GetCategoryAccountsAsync(category, "all", ct);
			return accounts.Select(account => (Category: category, Account: account)).ToList();
		});

		return [.. (await Task.WhenAll(fetches)).SelectMany(items => items)];
	}

	private static IEnumerable<(AssetCategory Category, Account Account)> RelatedComponents(
		IEnumerable<(AssetCategory Category, Account Account)> components,
		AssetCategory requestedCategory,
		Account requestedAccount)
	{
		if (!string.IsNullOrWhiteSpace(requestedAccount.Id))
			return components.Where(component =>
				string.Equals(component.Account.Id, requestedAccount.Id, StringComparison.OrdinalIgnoreCase));

		return components.Where(component =>
			component.Category == requestedCategory && ReferenceEquals(component.Account, requestedAccount));
	}

	private static string AccountKey(AssetCategory category, Account account) =>
		!string.IsNullOrWhiteSpace(account.Id)
			? $"id:{account.Id}"
			: $"{category}:{account.Slug}:{account.Name}";

	[McpServerTool(Name = "get_crypto_holdings", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get active-profile crypto and fiat positions grouped by crypto account, with a current-value subtotal")]
	public async Task<CryptoHoldingsResponse> GetCryptoHoldings(
		[Description("Zero-based position offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum positions to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var accounts = await api.GetCategoryAccountsAsync(AssetCategory.Cryptos, ct: ct);
		var mapped = accounts.Select(McpMapper.CurrencyAccount).ToList();
		var page = PagePositions(mapped, offset, limit);
		var totalValue = accounts
			.SelectMany(account => (account.Cryptos ?? []).Concat(account.Fiats ?? []))
			.Sum(position => position.DisplayCurrentValue ?? position.CurrentValue ?? 0m);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return new CryptoHoldingsResponse
		{
			DisplayCurrency = currency,
			TotalValue = McpMapper.Decimal(totalValue)!,
			AccountCount = mapped.Count,
			PositionCount = page.Total,
			ReturnedPositionCount = page.Returned,
			Offset = offset,
			Limit = limit,
			HasMore = page.HasMore,
			Accounts = page.Accounts
		};
	}

	private static PositionPage PagePositions(
		IReadOnlyList<HoldingAccountItem> accounts,
		int offset,
		int limit)
	{
		var flattened = accounts
			.SelectMany((account, accountIndex) =>
				account.Positions.Select(position => (AccountIndex: accountIndex, Position: position)))
			.ToList();
		var rows = flattened.Skip(offset).Take(limit).ToList();
		var byAccount = rows.GroupBy(row => row.AccountIndex).ToDictionary(
			group => group.Key,
			group => group.Select(row => row.Position).ToList());
		var pagedAccounts = byAccount
			.OrderBy(group => group.Key)
			.Select(group => accounts[group.Key] with { Positions = group.Value })
			.ToList();

		return new PositionPage(
			pagedAccounts,
			flattened.Count,
			rows.Count,
			(long)offset + limit < flattened.Count);
	}

	private sealed record PositionPage(
		List<HoldingAccountItem> Accounts,
		int Total,
		int Returned,
		bool HasMore);
}
