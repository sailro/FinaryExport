using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using FinaryExport.Models;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class TransactionTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_transactions", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get a bounded page of active-profile transactions for one supported category. Period filters are enforced with explicit dates.")]
	public async Task<TransactionsResponse> GetTransactions(
		[Description("Asset category. Only these support transactions: checkings, savings, investments, credits"), AllowedValues("checkings", "savings", "investments", "credits")] string category,
		[Description("Trailing time period. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: 1m"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period = "1m",
		[Description("Zero-based transaction offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum transactions to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		var cat = AccountTools.ParseCategory(category);

		if (!cat.HasTransactions())
			throw McpErrors.InvalidInput(
				$"Category '{category}' does not support transactions. Only checkings, savings, investments, and credits have transaction data.");

		period = ToolInputs.Period(period);
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var transactions = await api.GetCategoryTransactionsPageAsync(cat, period, offset, limit + 1, ct);
		var items = transactions.Select(transaction => McpMapper.Transaction(transaction, cat.ToUrlSegment()));
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Transactions(items, period, currency, offset, limit);
	}

	[McpServerTool(Name = "get_all_transactions", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get one bounded, date-sorted page across all active-profile transaction categories. Period filters are enforced with explicit dates.")]
	public async Task<TransactionsResponse> GetAllTransactions(
		[Description("Trailing time period. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: 1m"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period = "1m",
		[Description("Zero-based transaction offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum transactions to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		period = ToolInputs.Period(period);
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var perCategoryLimit = checked(offset + limit + 1);
		var fetches = Enum.GetValues<AssetCategory>().Where(c => c.HasTransactions()).Select(async category =>
		{
			try
			{
				var transactions = await api.GetCategoryTransactionsPageAsync(
					category,
					period,
					0,
					perCategoryLimit,
					ct);
				return (
					Items: transactions.Select(transaction =>
						McpMapper.Transaction(transaction, category.ToUrlSegment())).ToList(),
					Warning: (string?)null);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				return (
					Items: new List<TransactionItem>(),
					Warning: $"{category.ToUrlSegment()}: {ex.Message}");
			}
		});
		var fetched = await Task.WhenAll(fetches);
		var all = fetched.SelectMany(item => item.Items).ToList();
		var warnings = fetched.Select(item => item.Warning).Where(item => item is not null).Select(item => item!);

		var page = all
			.GroupBy(item => item.TransactionId is null
				? $"{item.Category}:{item.Date}:{item.AccountId}:{item.Name}:{item.Value}"
				: item.TransactionId.Value.ToString())
			.Select(group => group.First())
			.OrderByDescending(item => item.Date)
			.Skip(offset)
			.Take(limit + 1);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Transactions(page, period, currency, offset, limit, warnings);
	}
}
