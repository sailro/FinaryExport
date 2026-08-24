using FinaryExport.Models;
using FinaryExport.Models.Transactions;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
	public async Task<List<Transaction>> GetCategoryTransactionsAsync(AssetCategory category, string period = Defaults.DefaultPeriod, int pageSize = Defaults.DefaultTransactionPageSize, CancellationToken ct = default)
	{
		var (path, range, accountIds) = await BuildTransactionQueryAsync(category, period, ct);
		if (accountIds is { Count: 0 }) return [];
		var transactions = await GetPaginatedListAsync<Transaction>(path, pageSize, ct);
		return FilterTransactions(transactions, range, accountIds);
	}

	public async Task<List<Transaction>> GetCategoryTransactionsPageAsync(
		AssetCategory category,
		string period,
		int offset,
		int limit,
		CancellationToken ct = default)
	{
		var (path, range, accountIds) = await BuildTransactionQueryAsync(category, period, ct);
		if (accountIds is { Count: 0 }) return [];
		var transactions = await GetPaginatedListPageAsync<Transaction>(
			path,
			Defaults.DefaultTransactionPageSize,
			offset,
			limit,
			ct);

		return FilterTransactions(transactions, range, accountIds);
	}

	private async Task<(string Path, (DateTimeOffset Start, DateTimeOffset End)? Range, HashSet<string>? AccountIds)>
		BuildTransactionQueryAsync(AssetCategory category, string period, CancellationToken ct)
	{
		var normalizedPeriod = FinaryPeriod.Validate(period);
		var range = FinaryPeriod.GetRange(normalizedPeriod);

		if (range is null)
			return (
				$"{BasePath}/portfolio/{category.ToUrlSegment()}/transactions",
				null,
				null);

		var accounts = await GetCategoryAccountsAsync(category, Defaults.DefaultPeriod, ct);
		var accountIds = accounts
			.Select(account => account.Id)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Select(id => id!)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		if (accountIds.Count == 0)
			return (string.Empty, range, accountIds);

		var ids = string.Join(",", accountIds.Select(Uri.EscapeDataString));
		var start = range.Value.Start.ToString("yyyy-MM-dd");
		var end = range.Value.End.ToString("yyyy-MM-dd");
		return (
			$"{BasePath}/transactions?account_id={ids}&start_date={start}&end_date={end}",
			range,
			accountIds);
	}

	private static List<Transaction> FilterTransactions(
		IEnumerable<Transaction> transactions,
		(DateTimeOffset Start, DateTimeOffset End)? range,
		HashSet<string>? accountIds)
	{
		return
		[
			.. transactions
				.Where(transaction =>
					(accountIds is null ||
					 (transaction.Account?.Id is not null && accountIds.Contains(transaction.Account.Id))) &&
					FinaryPeriod.Contains(transaction.Date, range))
				.OrderByDescending(transaction => transaction.Date)
		];
	}
}
