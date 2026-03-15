using FinaryExport.Models.Transactions;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
	public async Task<List<TransactionCategory>> GetTransactionCategoriesAsync(CancellationToken ct = default)
	{
		return await GetAsync<List<TransactionCategory>>($"{BasePath}/transaction_categories?included_in_analysis=true", ct) ?? [];
	}
}
