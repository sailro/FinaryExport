using FinaryExport.Models;
using FinaryExport.Models.Transactions;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
	public async Task<List<Transaction>> GetCategoryTransactionsAsync(AssetCategory category, string period = Defaults.DefaultPeriod, int pageSize = Defaults.DefaultTransactionPageSize, CancellationToken ct = default)
	{
		return await GetPaginatedListAsync<Transaction>($"{BasePath}/portfolio/{category.ToUrlSegment()}/transactions?period={period}", pageSize, ct);
	}
}
