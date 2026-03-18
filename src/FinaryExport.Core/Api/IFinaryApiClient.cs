using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using FinaryExport.Models.Transactions;
using FinaryExport.Models.User;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Api;

public interface IFinaryApiClient
{
	// Setup — resolves org_id and membership_id for the owner membership
	Task<(string OrgId, string MembershipId)> GetOrganizationContextAsync(CancellationToken ct = default);

	// Returns all profiles (memberships) the user has access to
	Task<List<FinaryProfile>> GetAllProfilesAsync(CancellationToken ct = default);

	// Switches the active org context for subsequent API calls
	void SetOrganizationContext(string orgId, string membershipId);

	// Portfolio
	Task<PortfolioSummary?> GetPortfolioAsync(string period = Defaults.DefaultPeriod, CancellationToken ct = default);
	Task<List<TimeseriesData>> GetPortfolioTimeseriesAsync(string period, string valueType = Defaults.DefaultValueType, CancellationToken ct = default);
	Task<DividendSummary?> GetPortfolioDividendsAsync(CancellationToken ct = default);
	Task<AllocationData?> GetGeographicalAllocationAsync(CancellationToken ct = default);
	Task<AllocationData?> GetSectorAllocationAsync(CancellationToken ct = default);
	Task<FeeSummary?> GetPortfolioFeesAsync(CancellationToken ct = default);

	// Category-generic endpoints
	Task<List<Account>> GetCategoryAccountsAsync(Models.AssetCategory category, string period = Defaults.DefaultPeriod, CancellationToken ct = default);
	Task<List<TimeseriesData>> GetCategoryTimeseriesAsync(Models.AssetCategory category, string period = Defaults.DefaultPeriod, CancellationToken ct = default);
	Task<List<Transaction>> GetCategoryTransactionsAsync(Models.AssetCategory category, string period = Defaults.DefaultPeriod, int pageSize = Defaults.DefaultTransactionPageSize, CancellationToken ct = default);

	// Cross-cutting
	Task<List<HoldingsAccount>> GetHoldingsAccountsAsync(CancellationToken ct = default);
	Task<UserProfile?> GetCurrentUserAsync(CancellationToken ct = default);
}
