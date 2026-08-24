using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinaryExport.Api;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using FinaryExport.Models.Transactions;
using FinaryExport.Models.User;

namespace FinaryExport.Mcp.Contracts;

public sealed record UserProfileResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.AuthenticatedUserScope;

	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("email")]
	public string? Email { get; init; }

	[JsonPropertyName("country")]
	public string? Country { get; init; }

	[JsonPropertyName("subscription_status")]
	public string? SubscriptionStatus { get; init; }

	[JsonPropertyName("access_level")]
	public string? AccessLevel { get; init; }

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }
}

public sealed record ProfilesResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.AvailableProfilesScope;

	[JsonPropertyName("profiles")]
	public required List<ProfileItem> Profiles { get; init; }
}

public sealed record ProfileItem
{
	[JsonPropertyName("organization_id")]
	public required string OrganizationId { get; init; }

	[JsonPropertyName("membership_id")]
	public required string MembershipId { get; init; }

	[JsonPropertyName("name")]
	public required string Name { get; init; }
}

public sealed record ActiveProfileResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("organization_id")]
	public required string OrganizationId { get; init; }

	[JsonPropertyName("membership_id")]
	public required string MembershipId { get; init; }

	[JsonPropertyName("message")]
	public string Message { get; init; } = "Active profile changed for subsequent calls.";
}

public sealed record PortfolioResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("period")]
	public required string Period { get; init; }

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("created_at")]
	public string? CreatedAt { get; init; }

	[JsonPropertyName("gross_assets")]
	public required PortfolioMetric GrossAssets { get; init; }

	[JsonPropertyName("liabilities")]
	public required PortfolioMetric Liabilities { get; init; }

	[JsonPropertyName("net_worth")]
	public required PortfolioMetric NetWorth { get; init; }

	[JsonPropertyName("financial_assets")]
	public required PortfolioMetric FinancialAssets { get; init; }

	[JsonPropertyName("categories")]
	public required List<PortfolioCategory> Categories { get; init; }

	[JsonPropertyName("has_unqualified_loans")]
	public bool HasUnqualifiedLoans { get; init; }

	[JsonPropertyName("has_unlinked_loans")]
	public bool HasUnlinkedLoans { get; init; }

	[JsonPropertyName("reading_note")]
	public string ReadingNote { get; init; } =
		"These are authoritative totals for the active profile. Do not recompute them by summing account rows.";
}

public sealed record PortfolioMetric
{
	[JsonPropertyName("amount")]
	public string? Amount { get; init; }

	[JsonPropertyName("period_evolution")]
	public string? PeriodEvolution { get; init; }

	[JsonPropertyName("period_evolution_percent")]
	public string? PeriodEvolutionPercent { get; init; }
}

public sealed record PortfolioCategory
{
	[JsonPropertyName("category")]
	public required string Category { get; init; }

	[JsonPropertyName("kind")]
	public required string Kind { get; init; }

	[JsonPropertyName("amount")]
	public string? Amount { get; init; }

	[JsonPropertyName("share_percent")]
	public string? SharePercent { get; init; }

	[JsonPropertyName("period_evolution_percent")]
	public string? PeriodEvolutionPercent { get; init; }
}

public sealed record TimeseriesResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("period")]
	public required string Period { get; init; }

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("total_points")]
	public int TotalPoints { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("series")]
	public required List<TimeseriesSeries> Series { get; init; }

	[JsonPropertyName("points")]
	public required List<TimeseriesPoint> Points { get; init; }
}

public sealed record TimeseriesSeries
{
	[JsonPropertyName("label")]
	public string? Label { get; init; }

	[JsonPropertyName("current_amount")]
	public string? CurrentAmount { get; init; }

	[JsonPropertyName("period_evolution")]
	public string? PeriodEvolution { get; init; }

	[JsonPropertyName("period_evolution_percent")]
	public string? PeriodEvolutionPercent { get; init; }
}

public sealed record TimeseriesPoint
{
	[JsonPropertyName("series")]
	public string? Series { get; init; }

	[JsonPropertyName("date")]
	public required string Date { get; init; }

	[JsonPropertyName("value")]
	public required string Value { get; init; }
}

public sealed record AccountsResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("period")]
	public required string Period { get; init; }

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("total_unique_accounts")]
	public int TotalUniqueAccounts { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("accounts")]
	public required List<AccountItem> Accounts { get; init; }

	[JsonPropertyName("warnings")]
	public required List<string> Warnings { get; init; }

	[JsonPropertyName("reading_note")]
	public string ReadingNote { get; init; } =
		"Account rows are detail, not a portfolio total. The same account can appear in multiple categories; category values are grouped under one unique account. Use get_portfolio_summary for totals.";
}

public sealed record AccountItem
{
	[JsonPropertyName("account_id")]
	public string? AccountId { get; init; }

	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("institution")]
	public string? Institution { get; init; }

	[JsonPropertyName("native_currency")]
	public string? NativeCurrency { get; init; }

	[JsonPropertyName("iban")]
	public string? Iban { get; init; }

	[JsonPropertyName("state")]
	public string? State { get; init; }

	[JsonPropertyName("state_message")]
	public string? StateMessage { get; init; }

	[JsonPropertyName("sync_status")]
	public string? SyncStatus { get; init; }

	[JsonPropertyName("last_sync_at")]
	public string? LastSyncAt { get; init; }

	[JsonPropertyName("last_successful_sync_at")]
	public string? LastSuccessfulSyncAt { get; init; }

	[JsonPropertyName("annual_yield_percent")]
	public string? AnnualYieldPercent { get; init; }

	[JsonPropertyName("category_values")]
	public required List<AccountCategoryValue> CategoryValues { get; init; }
}

public sealed record AccountCategoryValue
{
	[JsonPropertyName("category")]
	public required string Category { get; init; }

	[JsonPropertyName("display_balance")]
	public string? DisplayBalance { get; init; }

	[JsonPropertyName("display_buying_value")]
	public string? DisplayBuyingValue { get; init; }

	[JsonPropertyName("unrealized_pnl")]
	public string? UnrealizedPnl { get; init; }
}

public sealed record HoldingsResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("account_count")]
	public int AccountCount { get; init; }

	[JsonPropertyName("position_count")]
	public int PositionCount { get; init; }

	[JsonPropertyName("returned_position_count")]
	public int ReturnedPositionCount { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("accounts")]
	public required List<HoldingAccountItem> Accounts { get; init; }

	[JsonPropertyName("reading_note")]
	public string ReadingNote { get; init; } =
		"Positions and account values are for the active profile. Shared household positions may be visible under another profile.";
}

public sealed record AccountPositionsResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("position_count")]
	public int PositionCount { get; init; }

	[JsonPropertyName("returned_position_count")]
	public int ReturnedPositionCount { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("account")]
	public required HoldingAccountItem Account { get; init; }

	[JsonPropertyName("reading_note")]
	public string ReadingNote { get; init; } =
		"Positions are limited to the active profile. Use get_profiles and set_active_profile to inspect another family member.";
}

public sealed record CryptoHoldingsResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("total_value")]
	public string TotalValue { get; init; } = "0";

	[JsonPropertyName("account_count")]
	public int AccountCount { get; init; }

	[JsonPropertyName("position_count")]
	public int PositionCount { get; init; }

	[JsonPropertyName("returned_position_count")]
	public int ReturnedPositionCount { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("accounts")]
	public required List<HoldingAccountItem> Accounts { get; init; }
}

public sealed record HoldingAccountItem
{
	[JsonPropertyName("account_id")]
	public string? AccountId { get; init; }

	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("institution")]
	public string? Institution { get; init; }

	[JsonPropertyName("display_balance")]
	public string? DisplayBalance { get; init; }

	[JsonPropertyName("positions")]
	public required List<PositionItem> Positions { get; init; }
}

public sealed record PositionItem
{
	[JsonPropertyName("kind")]
	public required string Kind { get; init; }

	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("code")]
	public string? Code { get; init; }

	[JsonPropertyName("isin")]
	public string? Isin { get; init; }

	[JsonPropertyName("symbol")]
	public string? Symbol { get; init; }

	[JsonPropertyName("asset_type")]
	public string? AssetType { get; init; }

	[JsonPropertyName("quantity")]
	public string? Quantity { get; init; }

	[JsonPropertyName("current_price")]
	public string? CurrentPrice { get; init; }

	[JsonPropertyName("current_value")]
	public string? CurrentValue { get; init; }

	[JsonPropertyName("buying_price")]
	public string? BuyingPrice { get; init; }

	[JsonPropertyName("buying_value")]
	public string? BuyingValue { get; init; }

	[JsonPropertyName("unrealized_pnl")]
	public string? UnrealizedPnl { get; init; }

	[JsonPropertyName("unrealized_pnl_percent")]
	public string? UnrealizedPnlPercent { get; init; }
}

public sealed record TransactionsResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("period")]
	public required string Period { get; init; }

	[JsonPropertyName("start_date")]
	public string? StartDate { get; init; }

	[JsonPropertyName("end_date")]
	public string? EndDate { get; init; }

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("returned_count")]
	public int ReturnedCount { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("transactions")]
	public required List<TransactionItem> Transactions { get; init; }

	[JsonPropertyName("warnings")]
	public required List<string> Warnings { get; init; }
}

public sealed record TransactionItem
{
	[JsonPropertyName("category")]
	public required string Category { get; init; }

	[JsonPropertyName("transaction_id")]
	public long? TransactionId { get; init; }

	[JsonPropertyName("date")]
	public string? Date { get; init; }

	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("value")]
	public string? Value { get; init; }

	[JsonPropertyName("transaction_type")]
	public string? TransactionType { get; init; }

	[JsonPropertyName("commission")]
	public string? Commission { get; init; }

	[JsonPropertyName("currency")]
	public string? Currency { get; init; }

	[JsonPropertyName("account_id")]
	public string? AccountId { get; init; }

	[JsonPropertyName("account_name")]
	public string? AccountName { get; init; }

	[JsonPropertyName("institution")]
	public string? Institution { get; init; }

	[JsonPropertyName("transaction_category")]
	public string? TransactionCategory { get; init; }
}

public sealed record DividendsResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("annual_income")]
	public string? AnnualIncome { get; init; }

	[JsonPropertyName("past_income")]
	public string? PastIncome { get; init; }

	[JsonPropertyName("projected_next_year")]
	public string? ProjectedNextYear { get; init; }

	[JsonPropertyName("yield_percent")]
	public string? YieldPercent { get; init; }

	[JsonPropertyName("total_events")]
	public int TotalEvents { get; init; }

	[JsonPropertyName("offset")]
	public int Offset { get; init; }

	[JsonPropertyName("limit")]
	public int Limit { get; init; }

	[JsonPropertyName("has_more")]
	public bool HasMore { get; init; }

	[JsonPropertyName("events")]
	public required List<DividendEvent> Events { get; init; }
}

public sealed record DividendEvent
{
	[JsonPropertyName("kind")]
	public required string Kind { get; init; }

	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("amount")]
	public string? Amount { get; init; }

	[JsonPropertyName("date")]
	public string? Date { get; init; }

	[JsonPropertyName("status")]
	public string? Status { get; init; }

	[JsonPropertyName("asset_type")]
	public string? AssetType { get; init; }
}

public sealed record FeesResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("annual_fees")]
	public string? AnnualFees { get; init; }

	[JsonPropertyName("annual_fees_percent")]
	public string? AnnualFeesPercent { get; init; }

	[JsonPropertyName("cumulated_fees")]
	public string? CumulatedFees { get; init; }

	[JsonPropertyName("cumulated_fees_percent")]
	public string? CumulatedFeesPercent { get; init; }

	[JsonPropertyName("contracts")]
	public required List<FeeContract> Contracts { get; init; }
}

public sealed record FeeContract
{
	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("institution")]
	public string? Institution { get; init; }

	[JsonPropertyName("cumulated_potential_savings")]
	public string? CumulatedPotentialSavings { get; init; }

	[JsonPropertyName("cumulated_potential_savings_percent")]
	public string? CumulatedPotentialSavingsPercent { get; init; }
}

public sealed record AllocationResponse
{
	[JsonPropertyName("scope")]
	public string Scope { get; init; } = McpMapper.ActiveProfileScope;

	[JsonPropertyName("display_currency")]
	public string? DisplayCurrency { get; init; }

	[JsonPropertyName("total")]
	public string? Total { get; init; }

	[JsonPropertyName("entries")]
	public required List<AllocationItem> Entries { get; init; }
}

public sealed record AllocationItem
{
	[JsonPropertyName("label")]
	public string? Label { get; init; }

	[JsonPropertyName("amount")]
	public string? Amount { get; init; }

	[JsonPropertyName("share_percent")]
	public string? SharePercent { get; init; }

	[JsonPropertyName("children")]
	public required List<AllocationItem> Children { get; init; }
}

public static class McpMapper
{
	public const string ActiveProfileScope = "active_profile";
	public const string AuthenticatedUserScope = "authenticated_user";
	public const string AvailableProfilesScope = "available_profiles";

	public static string? Decimal(decimal? value) =>
		value?.ToString(CultureInfo.InvariantCulture);

	public static string? DisplayCurrency(UserProfile? user) =>
		user?.UiConfiguration?.DisplayCurrency?.Code;

	public static UserProfileResponse User(UserProfile? user) => new()
	{
		Name = user?.Fullname,
		Email = user?.Email,
		Country = user?.Country,
		SubscriptionStatus = user?.SubscriptionStatus,
		AccessLevel = user?.AccessLevel,
		DisplayCurrency = DisplayCurrency(user)
	};

	public static ProfilesResponse Profiles(IEnumerable<FinaryProfile> profiles) => new()
	{
		Profiles =
		[
			.. profiles.Select(profile => new ProfileItem
			{
				OrganizationId = profile.OrgId,
				MembershipId = profile.MembershipId,
				Name = profile.ProfileName
			})
		]
	};

	public static PortfolioResponse Portfolio(
		PortfolioSummary? portfolio,
		string period,
		string? displayCurrency)
	{
		var categories = ExtractPortfolioCategories(portfolio?.Gross?.Assets, "asset")
			.Concat(ExtractPortfolioCategories(portfolio?.Gross?.Liabilities, "liability"))
			.ToList();
		var gross = portfolio?.Gross?.Total?.DisplayAmount ?? portfolio?.Gross?.Total?.Amount;
		var net = portfolio?.Net?.Total?.DisplayAmount ?? portfolio?.Net?.Total?.Amount;
		var liabilities = gross is not null && net is not null ? gross - net : null;

		return new PortfolioResponse
		{
			Period = period,
			DisplayCurrency = displayCurrency,
			CreatedAt = portfolio?.CreatedAt?.ToString("O"),
			GrossAssets = Metric(portfolio?.Gross?.Total),
			Liabilities = new PortfolioMetric { Amount = Decimal(liabilities) },
			NetWorth = Metric(portfolio?.Net?.Total),
			FinancialAssets = Metric(portfolio?.Finary?.Total),
			Categories = categories,
			HasUnqualifiedLoans = portfolio?.HasUnqualifiedLoans ?? false,
			HasUnlinkedLoans = portfolio?.HasUnlinkedLoans ?? false
		};
	}

	private static PortfolioMetric Metric(PortfolioTotalValues? total) => new()
	{
		Amount = Decimal(total?.DisplayAmount ?? total?.Amount),
		PeriodEvolution = Decimal(total?.DisplayValueDifference ?? total?.PeriodEvolution),
		PeriodEvolutionPercent = Decimal(total?.DisplayValueEvolution ?? total?.PeriodEvolutionPercent)
	};

	private static IEnumerable<PortfolioCategory> ExtractPortfolioCategories(
		JsonElement? source,
		string kind)
	{
		if (source is not { ValueKind: JsonValueKind.Object }) yield break;

		foreach (var property in source.Value.EnumerateObject())
		{
			var value = property.Value;
			yield return new PortfolioCategory
			{
				Category = NormalizePortfolioCategory(property.Name),
				Kind = kind,
				Amount = JsonDecimal(value, "display_amount") ?? JsonDecimal(value, "amount"),
				SharePercent = JsonDecimal(value, "share"),
				PeriodEvolutionPercent = JsonDecimal(value, "period_evolution_percent")
			};
		}
	}

	private static string NormalizePortfolioCategory(string category) => category switch
	{
		"checking_accounts" => "checkings",
		"investment_accounts" => "investments",
		"credit_accounts" => "credits",
		_ => category.Replace("_accounts", string.Empty, StringComparison.Ordinal)
	};

	public static TimeseriesResponse Timeseries(
		IEnumerable<TimeseriesData> source,
		string period,
		string? displayCurrency,
		int offset,
		int limit)
	{
		var data = source.ToList();
		var points = data.SelectMany(ParsePoints).ToList();
		return new TimeseriesResponse
		{
			Period = period,
			DisplayCurrency = displayCurrency,
			TotalPoints = points.Count,
			Offset = offset,
			Limit = limit,
			HasMore = (long)offset + limit < points.Count,
			Series =
			[
				.. data.Select(item => new TimeseriesSeries
				{
					Label = item.Label,
					CurrentAmount = Decimal(item.DisplayAmount ?? item.Balance),
					PeriodEvolution = Decimal(item.DisplayValueDifference ?? item.PeriodEvolution),
					PeriodEvolutionPercent = Decimal(item.DisplayValueEvolution ?? item.PeriodEvolutionPercent)
				})
			],
			Points = [.. points.Skip(offset).Take(limit)]
		};
	}

	private static IEnumerable<TimeseriesPoint> ParsePoints(TimeseriesData series)
	{
		if (series.Timeseries is not { ValueKind: JsonValueKind.Array }) yield break;

		foreach (var row in series.Timeseries.Value.EnumerateArray())
		{
			if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 2) continue;
			var date = row[0].ValueKind == JsonValueKind.String ? row[0].GetString() : null;
			var value = JsonDecimal(row[1]);
			if (date is null || value is null) continue;

			yield return new TimeseriesPoint { Series = series.Label, Date = date, Value = value };
		}
	}

	public static AccountsResponse Accounts(
		IEnumerable<(string Category, Account Account)> source,
		string period,
		string? displayCurrency,
		int offset,
		int limit,
		IEnumerable<string>? warnings = null,
		IEnumerable<AccountSynchronization>? synchronizations = null)
	{
		var syncItems = synchronizations?.ToList() ?? [];
		var grouped = source
			.GroupBy(
				item => item.Account.Id ?? $"{item.Category}:{item.Account.Slug}:{item.Account.Name}",
				StringComparer.OrdinalIgnoreCase)
			.Select(group =>
			{
				var first = group.First().Account;
				var synchronization = FindSynchronization(first, syncItems);
				return new AccountItem
				{
					AccountId = first.Id,
					Name = first.Name,
					Institution = first.Institution?.Name,
					NativeCurrency = first.Currency?.Code,
					Iban = first.Iban,
					State = synchronization?.State ?? synchronization?.ConnectionState ?? first.State,
					StateMessage = synchronization?.StateMessage ?? first.StateMessage,
					SyncStatus = synchronization?.SyncStatus,
					LastSyncAt = (synchronization?.LastSyncAt ?? first.LastSyncAt)?.ToString("O"),
					LastSuccessfulSyncAt = (synchronization?.LastSuccessfulSyncAt ?? first.LastSuccessfulSyncAt)?.ToString("O"),
					AnnualYieldPercent = Decimal(first.AnnualYield),
					CategoryValues =
					[
						.. group
							.GroupBy(item => item.Category)
							.Select(categoryGroup => categoryGroup.First())
							.Select(item => new AccountCategoryValue
							{
								Category = item.Category,
								DisplayBalance = Decimal(item.Account.DisplayBalance ?? item.Account.Balance),
								DisplayBuyingValue = Decimal(item.Account.DisplayBuyingValue ?? item.Account.BuyingValue),
								UnrealizedPnl = Decimal(item.Account.UnrealizedPnl)
							})
					]
				};
			})
			.OrderBy(account => account.Name)
			.ToList();

		return new AccountsResponse
		{
			Period = period,
			DisplayCurrency = displayCurrency,
			TotalUniqueAccounts = grouped.Count,
			Offset = offset,
			Limit = limit,
			HasMore = (long)offset + limit < grouped.Count,
			Accounts = [.. grouped.Skip(offset).Take(limit)],
			Warnings = [.. warnings ?? []]
		};
	}

	private static AccountSynchronization? FindSynchronization(
		Account account,
		IEnumerable<AccountSynchronization> synchronizations)
	{
		return synchronizations.FirstOrDefault(item =>
			(!string.IsNullOrWhiteSpace(account.CorrelationId) &&
			 string.Equals(item.CorrelationId, account.CorrelationId, StringComparison.OrdinalIgnoreCase)) ||
			(!string.IsNullOrWhiteSpace(account.ConnectionId) &&
			 string.Equals(item.CorrelationId, account.ConnectionId, StringComparison.OrdinalIgnoreCase)));
	}

	public static HoldingAccountItem InvestmentAccount(Account account) => new()
	{
		AccountId = account.Id,
		Name = account.Name,
		Institution = account.Institution?.Name,
		DisplayBalance = Decimal(account.DisplayBalance ?? account.Balance),
		Positions =
		[
			.. (account.Securities ?? []).Select(SecurityPosition),
			.. (account.Fiats ?? []).Select(position => CurrencyPosition(position, "fiat")),
			.. ParseScpiPositions(account.Scpis)
		]
	};

	public static HoldingAccountItem CurrencyAccount(Account account) => new()
	{
		AccountId = account.Id,
		Name = account.Name,
		Institution = account.Institution?.Name,
		DisplayBalance = Decimal(account.DisplayBalance ?? account.Balance),
		Positions =
		[
			.. (account.Cryptos ?? []).Select(position => CurrencyPosition(position, "crypto")),
			.. (account.Fiats ?? []).Select(position => CurrencyPosition(position, "fiat"))
		]
	};

	private static PositionItem SecurityPosition(SecurityPosition position) => new()
	{
		Kind = "security",
		Name = position.Security?.Name,
		Isin = position.Security?.Isin,
		Symbol = position.Security?.Symbol,
		AssetType = position.Security?.SecurityType,
		Quantity = Decimal(position.Quantity),
		CurrentPrice = Decimal(position.Security?.DisplayCurrentPrice ?? position.Security?.CurrentPrice),
		CurrentValue = Decimal(position.DisplayCurrentValue ?? position.CurrentValue),
		BuyingPrice = Decimal(position.DisplayBuyingPrice ?? position.BuyingPrice),
		BuyingValue = Decimal(position.DisplayBuyingValue ?? position.BuyingValue),
		UnrealizedPnl = Decimal(position.DisplayCurrentUpnl ?? position.CurrentUpnl ?? position.UnrealizedPnl),
		UnrealizedPnlPercent = Decimal(position.DisplayCurrentUpnlPercent ?? position.CurrentUpnlPercent ?? position.UnrealizedPnlPercent)
	};

	private static PositionItem CurrencyPosition(CurrencyPosition position, string kind) => new()
	{
		Kind = kind,
		Name = position.Asset?.Name,
		Code = position.Asset?.Code,
		Symbol = position.Asset?.Symbol,
		Quantity = Decimal(position.Quantity),
		CurrentPrice = Decimal(position.DisplayCurrentPrice ?? position.CurrentPrice),
		CurrentValue = Decimal(position.DisplayCurrentValue ?? position.CurrentValue),
		BuyingPrice = Decimal(position.DisplayBuyingPrice ?? position.BuyingPrice),
		BuyingValue = Decimal(position.DisplayBuyingValue ?? position.BuyingValue),
		UnrealizedPnl = Decimal(position.DisplayUnrealizedPnl ?? position.UnrealizedPnl),
		UnrealizedPnlPercent = Decimal(position.UnrealizedPnlPercent)
	};

	private static IEnumerable<PositionItem> ParseScpiPositions(JsonElement? source)
	{
		if (source is null) yield break;

		foreach (var item in EnumeratePositionObjects(source.Value))
		{
			yield return new PositionItem
			{
				Kind = "scpi",
				Name = FirstJsonString(item, ["scpi", "name"], ["asset", "name"], ["name"]),
				Code = FirstJsonString(item, ["scpi", "code"], ["asset", "code"], ["code"]),
				Symbol = FirstJsonString(item, ["scpi", "symbol"], ["asset", "symbol"], ["symbol"]),
				AssetType = FirstJsonString(item, ["scpi", "type"], ["asset", "type"], ["asset_type"]) ?? "scpi",
				Quantity = FirstJsonDecimal(item, ["quantity"], ["shares"], ["parts"], ["share_count"]),
				CurrentPrice = FirstJsonDecimal(item, ["display_current_price"], ["current_price"], ["scpi", "display_current_price"], ["scpi", "current_price"]),
				CurrentValue = FirstJsonDecimal(item, ["display_current_value"], ["current_value"], ["display_balance"], ["balance"]),
				BuyingPrice = FirstJsonDecimal(item, ["display_buying_price"], ["buying_price"]),
				BuyingValue = FirstJsonDecimal(item, ["display_buying_value"], ["buying_value"]),
				UnrealizedPnl = FirstJsonDecimal(item, ["display_current_upnl"], ["current_upnl"], ["display_unrealized_pnl"], ["unrealized_pnl"]),
				UnrealizedPnlPercent = FirstJsonDecimal(item, ["display_current_upnl_percent"], ["current_upnl_percent"], ["unrealized_pnl_percent"])
			};
		}
	}

	private static IEnumerable<JsonElement> EnumeratePositionObjects(JsonElement source)
	{
		if (source.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in source.EnumerateArray())
				if (item.ValueKind == JsonValueKind.Object)
					yield return item;
			yield break;
		}

		if (source.ValueKind != JsonValueKind.Object) yield break;

		foreach (var containerName in new[] { "data", "items", "positions", "scpis" })
		{
			if (!source.TryGetProperty(containerName, out var container) || container.ValueKind != JsonValueKind.Array)
				continue;

			foreach (var item in container.EnumerateArray())
				if (item.ValueKind == JsonValueKind.Object)
					yield return item;
			yield break;
		}

		yield return source;
	}

	private static string? FirstJsonDecimal(JsonElement source, params string[][] paths)
	{
		foreach (var path in paths)
		{
			var value = JsonElementAt(source, path);
			if (value is not null)
			{
				var result = JsonDecimal(value.Value);
				if (result is not null) return result;
			}
		}

		return null;
	}

	private static string? FirstJsonString(JsonElement source, params string[][] paths)
	{
		foreach (var path in paths)
		{
			var value = JsonElementAt(source, path);
			if (value is { ValueKind: JsonValueKind.String }) return value.Value.GetString();
		}

		return null;
	}

	private static JsonElement? JsonElementAt(JsonElement source, IReadOnlyList<string> path)
	{
		var current = source;
		foreach (var segment in path)
		{
			if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
				return null;
		}

		return current;
	}

	public static TransactionItem Transaction(Transaction transaction, string category) => new()
	{
		Category = category,
		TransactionId = transaction.Id,
		Date = transaction.Date,
		Name = transaction.DisplayName ?? transaction.Name,
		Value = Decimal(transaction.DisplayValue ?? transaction.Value),
		TransactionType = transaction.TransactionType,
		Commission = Decimal(transaction.Commission),
		Currency = transaction.Currency?.Code,
		AccountId = transaction.Account?.Id,
		AccountName = transaction.Account?.Name,
		Institution = transaction.Institution?.Name,
		TransactionCategory = transaction.Category?.Name
	};

	public static TransactionsResponse Transactions(
		IEnumerable<TransactionItem> source,
		string period,
		string? displayCurrency,
		int offset,
		int limit,
		IEnumerable<string>? warnings = null)
	{
		var range = FinaryPeriod.GetRange(period);
		var items = source.OrderByDescending(item => item.Date).Take(limit + 1).ToList();
		return new TransactionsResponse
		{
			Period = period,
			StartDate = range?.Start.ToString("yyyy-MM-dd"),
			EndDate = range?.End.ToString("yyyy-MM-dd"),
			DisplayCurrency = displayCurrency,
			Offset = offset,
			Limit = limit,
			ReturnedCount = Math.Min(items.Count, limit),
			HasMore = items.Count > limit,
			Transactions = [.. items.Take(limit)],
			Warnings = [.. warnings ?? []]
		};
	}

	public static DividendsResponse Dividends(
		DividendSummary? summary,
		string? displayCurrency,
		int offset,
		int limit)
	{
		var past = (summary?.PastDividends ?? []).Select(item => Dividend(item, "past"));
		var upcoming = (summary?.UpcomingDividends ?? []).Select(item => Dividend(item, "upcoming"));
		var events = past.Concat(upcoming).OrderByDescending(item => item.Date).ToList();
		return new DividendsResponse
		{
			DisplayCurrency = displayCurrency,
			AnnualIncome = Decimal(summary?.AnnualIncome),
			PastIncome = Decimal(summary?.PastIncome),
			ProjectedNextYear = Decimal(summary?.NextYear?.Sum(item => item.Value ?? 0m)),
			YieldPercent = Decimal(summary?.Yield),
			TotalEvents = events.Count,
			Offset = offset,
			Limit = limit,
			HasMore = (long)offset + limit < events.Count,
			Events = [.. events.Skip(offset).Take(limit)]
		};
	}

	private static DividendEvent Dividend(DividendEntry item, string kind) => new()
	{
		Kind = kind,
		Name = item.Holding?.Name ?? item.Asset?.Name,
		Amount = Decimal(item.DisplayAmount ?? item.Amount),
		Date = item.PaymentAt ?? item.ReceivedAt ?? item.ExDividendAt,
		Status = item.Status,
		AssetType = item.AssetSubtype ?? item.AssetType
	};

	public static FeesResponse Fees(FeeSummary? summary, string? displayCurrency) => new()
	{
		DisplayCurrency = displayCurrency,
		AnnualFees = Decimal(summary?.Total?.AnnualFeesAmount),
		AnnualFeesPercent = Decimal(summary?.Total?.AnnualFeesPercent),
		CumulatedFees = Decimal(summary?.Total?.CumulatedFeesAmount),
		CumulatedFeesPercent = Decimal(summary?.Total?.CumulatedFeesPercent),
		Contracts = ParseFeeContracts(summary?.Data)
	};

	private static List<FeeContract> ParseFeeContracts(JsonElement? data)
	{
		if (data is not { ValueKind: JsonValueKind.Array }) return [];
		return
		[
			.. data.Value.EnumerateArray().Select(item => new FeeContract
			{
				Name = JsonString(item, "contract", "name"),
				Institution = JsonString(item, "contract", "institution", "name"),
				CumulatedPotentialSavings = JsonDecimal(item, "cumulated_potential_savings_amount"),
				CumulatedPotentialSavingsPercent = JsonDecimal(item, "cumulated_potential_savings_percent")
			})
		];
	}

	public static AllocationResponse Allocation(AllocationData? data, string? displayCurrency) => new()
	{
		DisplayCurrency = displayCurrency,
		Total = Decimal(data?.Total),
		Entries = [.. (data?.Distribution ?? []).Select(Allocation)]
	};

	private static AllocationItem Allocation(AllocationEntry item) => new()
	{
		Label = item.Label,
		Amount = Decimal(item.Amount),
		SharePercent = Decimal(item.Share),
		Children = [.. (item.Distribution ?? []).Select(Allocation)]
	};

	private static string? JsonDecimal(JsonElement parent, string propertyName)
	{
		return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(propertyName, out var value)
			? JsonDecimal(value)
			: null;
	}

	private static string? JsonDecimal(JsonElement value)
	{
		return value.ValueKind switch
		{
			JsonValueKind.Number => value.GetRawText(),
			JsonValueKind.String => value.GetString(),
			_ => null
		};
	}

	private static string? JsonString(JsonElement source, params string[] path)
	{
		var current = source;
		foreach (var segment in path)
		{
			if (current.ValueKind != JsonValueKind.Object ||
				!current.TryGetProperty(segment, out current))
				return null;
		}

		return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
	}
}
