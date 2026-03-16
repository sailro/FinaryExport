using FinaryExport.Models;
using FinaryExport.Models.Transactions;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
	public async Task<List<Transaction>> GetCategoryTransactionsAsync(AssetCategory category, string period = "all", int pageSize = 200, CancellationToken ct = default)
	{
		return await GetPaginatedListAsync<Transaction>($"{BasePath}/portfolio/{category.ToUrlSegment()}/transactions?period={period}", pageSize, ct);
	}
}
