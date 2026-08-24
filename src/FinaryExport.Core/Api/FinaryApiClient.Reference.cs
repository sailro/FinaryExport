using FinaryExport.Models.Accounts;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
	public async Task<List<HoldingsAccount>> GetHoldingsAccountsAsync(CancellationToken ct = default)
	{
		return await GetAsync<List<HoldingsAccount>>($"{BasePath}/holdings_accounts?with_transactions=true", ct) ?? [];
	}

	public async Task<List<AccountSynchronization>> GetSynchronizationsAsync(CancellationToken ct = default)
	{
		return await GetAsync<List<AccountSynchronization>>(FinaryConstants.ApiPaths.SynchronizationsPath, ct) ?? [];
	}
}
