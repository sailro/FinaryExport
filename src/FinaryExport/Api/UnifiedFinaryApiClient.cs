using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using FinaryExport.Models.Transactions;
using FinaryExport.Models.User;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Api;

// Decorator that aggregates data from ALL memberships for the unified export.
// Shared assets (same account ID across memberships) appear once at full value.
// Exclusive assets from each member are included.
// Sheet writers use this transparently — no changes needed downstream.
public sealed class UnifiedFinaryApiClient : IFinaryApiClient
{
	private readonly IFinaryApiClient _inner;
	private readonly List<FinaryProfile> _profiles;
	private readonly ILogger _logger;

	// Caches merged accounts per category to avoid redundant API calls.
	// The PortfolioSummarySheet and AccountsSheet both call GetCategoryAccountsAsync
	// for the same categories — the cache prevents fetching twice.
	private readonly Dictionary<(AssetCategory, string), List<Account>> _accountCache = [];

	public UnifiedFinaryApiClient(IFinaryApiClient inner, List<FinaryProfile> profiles, ILogger logger)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (profiles.Count == 0)
			throw new ArgumentException("At least one profile is required", nameof(profiles));
	}

	// ── Context management ──
	// No-op: unified mode manages context internally per method call.
	public void SetOrganizationContext(string orgId, string membershipId) { }

	// ── Delegated unchanged ──

	public Task<(string OrgId, string MembershipId)> GetOrganizationContextAsync(CancellationToken ct = default)
		=> _inner.GetOrganizationContextAsync(ct);

	public Task<List<FinaryProfile>> GetAllProfilesAsync(CancellationToken ct = default)
		=> _inner.GetAllProfilesAsync(ct);

	public Task<UserProfile?> GetCurrentUserAsync(CancellationToken ct = default)
	{
		UseOwnerContext();
		return _inner.GetCurrentUserAsync(ct);
	}

	// ── Aggregated: accounts merged by ID across all memberships ──

	public async Task<List<Account>> GetCategoryAccountsAsync(AssetCategory category, string period = "all", CancellationToken ct = default)
	{
		var cacheKey = (category, period);
		if (_accountCache.TryGetValue(cacheKey, out var cached))
			return cached;

		var merged = new Dictionary<string, Account>();

		foreach (var profile in _profiles)
		{
			_inner.SetOrganizationContext(profile.OrgId, profile.MembershipId);
			var accounts = await _inner.GetCategoryAccountsAsync(category, period, ct);

			foreach (var account in accounts)
			{
				if (account.Id is null) continue;

				if (merged.ContainsKey(account.Id))
					continue; // Already have this asset from another membership

				// Unified view uses display values (EUR-converted) as the base.
				// For shared assets (ownership < 100%), scale up: display_balance / share = full value.
				var myShare = account.OwnershipRepartition?
					.FirstOrDefault(o => o.Membership?.Id == profile.MembershipId)?.Share;

				if (myShare is > 0m and < 1m)
				{
					var fullBalance = account.DisplayBalance / myShare;
					var fullBuyingValue = account.DisplayBuyingValue / myShare;

					merged[account.Id] = account with
					{
						Balance = fullBalance,
						DisplayBalance = fullBalance,
						BuyingValue = fullBuyingValue,
						DisplayBuyingValue = fullBuyingValue
					};
				}
				else
				{
					// Exclusive asset (share=1 or no repartition).
					// Use display values (EUR-converted) for consistency.
					merged[account.Id] = account with
					{
						Balance = account.DisplayBalance ?? account.Balance,
						DisplayBalance = account.DisplayBalance ?? account.Balance,
						BuyingValue = account.DisplayBuyingValue ?? account.BuyingValue,
						DisplayBuyingValue = account.DisplayBuyingValue ?? account.BuyingValue
					};
				}
			}
		}

		var result = merged.Values.ToList();
		_accountCache[cacheKey] = result;

		_logger.LogDebug("Unified {Category}: {Count} unique accounts from {Profiles} profiles", category, result.Count, _profiles.Count);

		return result;
	}

	// ── Aggregated: synthetic portfolio summary from merged accounts ──

	public async Task<PortfolioSummary?> GetPortfolioAsync(
		string period = "all", CancellationToken ct = default)
	{
		// Fetch owner's portfolio for evolution metrics and metadata
		UseOwnerContext();
		var ownerPortfolio = await _inner.GetPortfolioAsync(period, ct);

		// Compute unified totals from merged accounts across all memberships
		var grossTotal = 0m;
		var creditsTotal = 0m;

		foreach (var category in Enum.GetValues<AssetCategory>())
		{
			var accounts = await GetCategoryAccountsAsync(category, period, ct);
			var categorySum = accounts.Sum(a => a.Balance ?? 0m);

			if (category == AssetCategory.Credits)
				creditsTotal = categorySum;
			else
				grossTotal += categorySum;
		}

		var netTotal = grossTotal - creditsTotal;

		// Build unified summary: corrected totals with owner's evolution data
		return new PortfolioSummary
		{
			CreatedAt = ownerPortfolio?.CreatedAt,
			Gross = new PortfolioValues
			{
				Total = new PortfolioTotalValues
				{
					Amount = grossTotal,
					DisplayAmount = grossTotal,
					Evolution = ownerPortfolio?.Gross?.Total?.Evolution,
					EvolutionPercent = ownerPortfolio?.Gross?.Total?.EvolutionPercent,
					PeriodEvolution = ownerPortfolio?.Gross?.Total?.PeriodEvolution,
					PeriodEvolutionPercent = ownerPortfolio?.Gross?.Total?.PeriodEvolutionPercent
				},
				Assets = ownerPortfolio?.Gross?.Assets,
				Liabilities = ownerPortfolio?.Gross?.Liabilities
			},
			Net = new PortfolioValues
			{
				Total = new PortfolioTotalValues
				{
					Amount = netTotal,
					DisplayAmount = netTotal,
					Evolution = ownerPortfolio?.Net?.Total?.Evolution,
					EvolutionPercent = ownerPortfolio?.Net?.Total?.EvolutionPercent,
					PeriodEvolution = ownerPortfolio?.Net?.Total?.PeriodEvolution,
					PeriodEvolutionPercent = ownerPortfolio?.Net?.Total?.PeriodEvolutionPercent
				},
				Assets = ownerPortfolio?.Net?.Assets,
				Liabilities = ownerPortfolio?.Net?.Liabilities
			},
			Finary = ownerPortfolio?.Finary
		};
	}

	// ── Aggregated: transactions merged by ID across all memberships ──

	public async Task<List<Transaction>> GetCategoryTransactionsAsync(
		AssetCategory category, string period = "all", int pageSize = 200, CancellationToken ct = default)
	{
		var merged = new Dictionary<long, Transaction>();

		foreach (var profile in _profiles)
		{
			_inner.SetOrganizationContext(profile.OrgId, profile.MembershipId);
			var transactions = await _inner.GetCategoryTransactionsAsync(category, period, pageSize, ct);

			foreach (var tx in transactions)
			{
				if (tx.Id is not null)
					merged.TryAdd(tx.Id.Value, tx);
			}
		}

		return [.. merged.Values.OrderByDescending(t => t.Date)];
	}

	// ── Aggregated: dividends with entries merged by ID ──

	public async Task<DividendSummary?> GetPortfolioDividendsAsync(CancellationToken ct = default)
	{
		var pastById = new Dictionary<int, DividendEntry>();
		var upcomingById = new Dictionary<int, DividendEntry>();
		DividendSummary? baseSummary = null;

		foreach (var profile in _profiles)
		{
			_inner.SetOrganizationContext(profile.OrgId, profile.MembershipId);
			var dividends = await _inner.GetPortfolioDividendsAsync(ct);
			if (dividends is null) continue;

			// Use owner's summary as the base (first profile)
			baseSummary ??= dividends;

			foreach (var div in dividends.PastDividends ?? [])
			{
				if (div.Id is not null)
					pastById.TryAdd(div.Id.Value, div);
			}

			foreach (var div in dividends.UpcomingDividends ?? [])
			{
				if (div.Id is not null)
					upcomingById.TryAdd(div.Id.Value, div);
			}
		}

		if (baseSummary is null) return null;

		var mergedPast = pastById.Values.ToList();
		var mergedUpcoming = upcomingById.Values.ToList();

		// Override entry lists and recompute PastIncome from merged entries.
		// AnnualIncome/Yield/NextYear use the owner's values as a baseline
		// since they include projections that can't be easily recomputed.
		return baseSummary with
		{
			PastDividends = mergedPast,
			UpcomingDividends = mergedUpcoming,
			PastIncome = mergedPast.Sum(d => d.Amount ?? 0m)
		};
	}

	// ── Aggregated: asset list merged by holding ID ──

	public async Task<List<AssetListEntry>> GetAssetListAsync(string period = "1d", CancellationToken ct = default)
	{
		var merged = new Dictionary<long, AssetListEntry>();

		foreach (var profile in _profiles)
		{
			_inner.SetOrganizationContext(profile.OrgId, profile.MembershipId);
			var entries = await _inner.GetAssetListAsync(period, ct);

			foreach (var entry in entries)
			{
				if (entry.HoldingId is null)
					continue;

				if (!merged.TryGetValue(entry.HoldingId.Value, out var existing) ||
					(entry.CurrentValue ?? 0m) > (existing.CurrentValue ?? 0m))
				{
					merged[entry.HoldingId.Value] = entry;
				}
			}
		}

		return [.. merged.Values];
	}

	// ── Aggregated: holdings accounts merged by ID ──

	public async Task<List<HoldingsAccount>> GetHoldingsAccountsAsync(CancellationToken ct = default)
	{
		var merged = new Dictionary<string, HoldingsAccount>();

		foreach (var profile in _profiles)
		{
			_inner.SetOrganizationContext(profile.OrgId, profile.MembershipId);
			var accounts = await _inner.GetHoldingsAccountsAsync(ct);

			foreach (var account in accounts)
			{
				if (account.Id is null) continue;

				if (!merged.TryGetValue(account.Id, out var existing) ||
					(account.Balance ?? 0m) > (existing.Balance ?? 0m))
				{
					merged[account.Id] = account;
				}
			}
		}

		return [.. merged.Values];
	}

	// ── Delegated to owner: timeseries, allocations, fees ──
	// These are analysis/metadata endpoints where meaningful cross-membership
	// aggregation isn't straightforward. The owner's view is used.

	public Task<List<TimeseriesData>> GetPortfolioTimeseriesAsync(string period, string valueType = "gross", CancellationToken ct = default)
	{
		UseOwnerContext();
		return _inner.GetPortfolioTimeseriesAsync(period, valueType, ct);
	}

	public Task<AllocationData?> GetGeographicalAllocationAsync(CancellationToken ct = default)
	{
		UseOwnerContext();
		return _inner.GetGeographicalAllocationAsync(ct);
	}

	public Task<AllocationData?> GetSectorAllocationAsync(CancellationToken ct = default)
	{
		UseOwnerContext();
		return _inner.GetSectorAllocationAsync(ct);
	}

	public Task<FeeSummary?> GetPortfolioFeesAsync(CancellationToken ct = default)
	{
		UseOwnerContext();
		return _inner.GetPortfolioFeesAsync(ct);
	}

	public Task<List<TimeseriesData>> GetCategoryTimeseriesAsync(AssetCategory category, string period = "all", CancellationToken ct = default)
	{
		UseOwnerContext();
		return _inner.GetCategoryTimeseriesAsync(category, period, ct);
	}

	// ── Helpers ──

	private void UseOwnerContext()
	{
		var owner = _profiles[0];
		_inner.SetOrganizationContext(owner.OrgId, owner.MembershipId);
	}
}
