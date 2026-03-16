using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models;
using FinaryExport.Models.Transactions;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class TransactionTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_transactions"), Description("Get transactions for a specific asset category. Only checkings, savings, investments, and credits support transactions — other categories will return an error.")]
	public async Task<List<Transaction>> GetTransactions(
		[Description("Asset category. Only these support transactions: checkings, savings, investments, credits")] string category,
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = "all",
		CancellationToken ct = default)
	{
		var cat = AccountTools.ParseCategory(category);

		if (!cat.HasTransactions())
			throw new ArgumentException(
				$"Category '{category}' does not support transactions. Only checkings, savings, investments, and credits have transaction data.");

		return await api.GetCategoryTransactionsAsync(cat, period, ct: ct);
	}

	[McpServerTool(Name = "get_all_transactions"), Description("Get transactions across ALL transaction-capable categories (checkings, savings, investments, credits) in a single call")]
	public async Task<List<Transaction>> GetAllTransactions(
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = "all",
		CancellationToken ct = default)
	{
		var all = new List<Transaction>();

		foreach (var category in Enum.GetValues<AssetCategory>().Where(c => c.HasTransactions()))
		{
			try
			{
				var transactions = await api.GetCategoryTransactionsAsync(category, period, ct: ct);
				all.AddRange(transactions);
			}
			catch
			{
				// Skip categories that fail — return what we can
			}
		}

		return all;
	}
}
