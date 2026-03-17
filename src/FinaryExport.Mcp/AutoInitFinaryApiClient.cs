using FinaryExport.Api;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using FinaryExport.Models.Transactions;
using FinaryExport.Models.User;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Mcp;

// Decorator that auto-initializes org context on the first data call.
// Without this, MCP users must manually call get_profiles + set_active_profile
// before any data tool works. This resolves the owner's default profile lazily.
public sealed class AutoInitFinaryApiClient(FinaryApiClient inner, ILogger<AutoInitFinaryApiClient> logger)
	: IFinaryApiClient
{
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private volatile bool _initialized;

	private async Task EnsureInitializedAsync(CancellationToken ct)
	{
		if (_initialized) return;

		await _initLock.WaitAsync(ct);
		try
		{
			if (_initialized) return;

			logger.LogInformation("Auto-initializing organization context for MCP session");
			await inner.GetOrganizationContextAsync(ct);
			_initialized = true;
		}
		finally
		{
			_initLock.Release();
		}
	}

	// Setup — auto-init before delegating to ensure auth is triggered
	public async Task<(string OrgId, string MembershipId)> GetOrganizationContextAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		var result = await inner.GetOrganizationContextAsync(ct);
		return result;
	}

	public async Task<List<FinaryProfile>> GetAllProfilesAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetAllProfilesAsync(ct);
	}

	public void SetOrganizationContext(string orgId, string membershipId)
	{
		inner.SetOrganizationContext(orgId, membershipId);
		_initialized = true;
	}

	public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetCurrentUserAsync(ct);
	}

	// Data endpoints — auto-init before delegating

	public async Task<PortfolioSummary?> GetPortfolioAsync(string period = "all", CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetPortfolioAsync(period, ct);
	}

	public async Task<List<TimeseriesData>> GetPortfolioTimeseriesAsync(string period, string valueType = "gross", CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetPortfolioTimeseriesAsync(period, valueType, ct);
	}

	public async Task<DividendSummary?> GetPortfolioDividendsAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetPortfolioDividendsAsync(ct);
	}

	public async Task<AllocationData?> GetGeographicalAllocationAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetGeographicalAllocationAsync(ct);
	}

	public async Task<AllocationData?> GetSectorAllocationAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetSectorAllocationAsync(ct);
	}

	public async Task<FeeSummary?> GetPortfolioFeesAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetPortfolioFeesAsync(ct);
	}

	public async Task<List<Account>> GetCategoryAccountsAsync(AssetCategory category, string period = "all", CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetCategoryAccountsAsync(category, period, ct);
	}

	public async Task<List<TimeseriesData>> GetCategoryTimeseriesAsync(AssetCategory category, string period = "all", CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetCategoryTimeseriesAsync(category, period, ct);
	}

	public async Task<List<Transaction>> GetCategoryTransactionsAsync(AssetCategory category, string period = "all", int pageSize = 200, CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetCategoryTransactionsAsync(category, period, pageSize, ct);
	}

	public async Task<List<HoldingsAccount>> GetHoldingsAccountsAsync(CancellationToken ct = default)
	{
		await EnsureInitializedAsync(ct);
		return await inner.GetHoldingsAccountsAsync(ct);
	}
}
