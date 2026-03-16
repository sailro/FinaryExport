# Finary MCP Tool Catalog

> **Author:** Livingston (Protocol Analyst)
> **Date:** 2026-03-14
> **Source:** `IFinaryApiClient` interface + all partial implementations + `api-analysis.md` wire capture
> **Purpose:** Complete API surface catalog for MCP server tool definitions

---

## Table of Contents

1. [Implemented API Methods — MCP Tool Candidates](#1-implemented-api-methods)
2. [Wire-Observed but Not Implemented — Future Candidates](#2-wire-observed-not-implemented)
3. [Model Type Catalog](#3-model-type-catalog)
4. [Pagination Patterns](#4-pagination-patterns)
5. [Context Requirements](#5-context-requirements)
6. [Safety Notes for Mutating Operations](#6-safety-notes)
7. [Recommendations](#7-recommendations)

---

## 1. Implemented API Methods — MCP Tool Candidates

These are the methods on `IFinaryApiClient` (defined in `src/FinaryExport/Api/IFinaryApiClient.cs`). All are **read-only** (GET). The existing client has **zero** mutating endpoints.

### 1.1 User & Profile Operations

| # | Method | HTTP Endpoint | Parameters | Return Type | Read/Write | MCP Tool Name | Description | Expose? |
|---|--------|--------------|------------|-------------|------------|---------------|-------------|---------|
| 1 | `GetCurrentUserAsync` | `GET /users/me` | — | `UserProfile?` | Read | `finary_get_current_user` | Get the authenticated user's profile (name, email, subscription status, display currency) | ✅ Yes |
| 2 | `GetAllProfilesAsync` | `GET /users/me/organizations` | — | `List<FinaryProfile>` | Read | `finary_list_profiles` | List all accessible profiles (memberships) across organizations | ✅ Yes |
| 3 | `GetOrganizationContextAsync` | `GET /users/me/organizations` | — | `(string OrgId, string MembershipId)` | Read | `finary_get_org_context` | Resolve the owner's org_id and membership_id (needed before most other calls) | ✅ Yes — **bootstrapping tool** |
| 4 | `SetOrganizationContext` | *(no HTTP call — sets internal state)* | `orgId`, `membershipId` | `void` | N/A | `finary_set_profile` | Switch which profile (membership) subsequent calls operate on | ✅ Yes — **context tool** |

### 1.2 Portfolio Overview Operations

| # | Method | HTTP Endpoint | Parameters | Return Type | Read/Write | MCP Tool Name | Description | Expose? |
|---|--------|--------------|------------|-------------|------------|---------------|-------------|---------|
| 5 | `GetPortfolioAsync` | `GET .../{m}/portfolio?new_format=true&period={period}` | `period` (default: `"all"`) | `PortfolioSummary?` | Read | `finary_get_portfolio` | Get portfolio summary — gross/net totals, evolution, period performance | ✅ Yes — **primary wealth overview** |
| 6 | `GetPortfolioTimeseriesAsync` | `GET .../{m}/portfolio/timeseries?new_format=true&period={period}&timeseries_type=sum&value_type={valueType}` | `period`, `valueType` (default: `"gross"`) | `List<TimeseriesData>` | Read | `finary_get_portfolio_timeseries` | Get portfolio value history over time (for charting/trend analysis) | ✅ Yes |
| 7 | `GetPortfolioDividendsAsync` | `GET .../{m}/portfolio/dividends?with_real_estate=true` | — | `DividendSummary?` | Read | `finary_get_dividends` | Get dividend income summary — annual, past, upcoming, per asset type | ✅ Yes |
| 8 | `GetGeographicalAllocationAsync` | `GET .../{m}/portfolio/geographical_allocation` | — | `AllocationData?` | Read | `finary_get_geo_allocation` | Get geographical allocation breakdown of portfolio | ✅ Yes |
| 9 | `GetSectorAllocationAsync` | `GET .../{m}/portfolio/sector_allocation` | — | `AllocationData?` | Read | `finary_get_sector_allocation` | Get sector allocation breakdown of portfolio | ✅ Yes |
| 10 | `GetPortfolioFeesAsync` | `GET .../{m}/portfolio/fees?new_format=true` | — | `FeeSummary?` | Read | `finary_get_fees` | Get fee analysis — annual/cumulated fees, savings, potential savings | ✅ Yes |

### 1.3 Category-Generic Operations

These are parameterized by `AssetCategory` enum. Each maps to a per-category API endpoint.

| # | Method | HTTP Endpoint | Parameters | Return Type | Read/Write | MCP Tool Name | Description | Expose? |
|---|--------|--------------|------------|-------------|------------|---------------|-------------|---------|
| 11 | `GetCategoryAccountsAsync` | `GET .../{m}/portfolio/{category}/accounts?period={period}` | `category` (enum), `period` (default: `"all"`) | `List<Account>` | Read | `finary_get_category_accounts` | List accounts for a specific asset category (checkings, savings, investments, etc.) | ✅ Yes — **core data** |
| 12 | `GetCategoryTimeseriesAsync` | `GET .../{m}/portfolio/{category}/timeseries?new_format=true&period={period}` | `category` (enum), `period` (default: `"all"`) | `List<TimeseriesData>` | Read | `finary_get_category_timeseries` | Get value history for a specific asset category | ✅ Yes |
| 13 | `GetCategoryTransactionsAsync` | `GET .../{m}/portfolio/{category}/transactions?period={period}` | `category` (enum), `period` (default: `"all"`), `pageSize` (default: 200) | `List<Transaction>` | Read | `finary_get_category_transactions` | Get transactions for a category. **Only 4 categories supported:** Checkings, Savings, Investments, Credits. Others return 404. | ✅ Yes — **high LLM value** |

### 1.4 Cross-Cutting Operations

| # | Method | HTTP Endpoint | Parameters | Return Type | Read/Write | MCP Tool Name | Description | Expose? |
|---|--------|--------------|------------|-------------|------------|---------------|-------------|---------|
| 14 | `GetAssetListAsync` | `GET .../{m}/asset_list?limit=100&period={period}` | `period` (default: `"all"`) | `List<AssetListEntry>` | Read | `finary_get_asset_list` | Get individual holdings/positions across all accounts — name, value, unrealized P&L | ✅ Yes — **high LLM value** |
| 15 | `GetHoldingsAccountsAsync` | `GET .../{m}/holdings_accounts?with_transactions=true` | — | `List<HoldingsAccount>` | Read | `finary_get_holdings_accounts` | Get all holdings accounts (cross-category account list with balances) | ✅ Yes |

---

## 2. Wire-Observed but Not Implemented — Future Candidates

These endpoints were observed in the traffic capture but are **not** on `IFinaryApiClient`. They could be added later. Sorted by LLM usefulness.

### High-Value Future Candidates

| Endpoint | HTTP Method | Description | Why Useful for LLM |
|----------|-------------|-------------|---------------------|
| `GET .../{m}/transactions` | GET | Organization-level transaction search with filters (`account_id`, `start_date`, `end_date`, `transaction_category_id`, `type`) | Powerful search — LLM could answer "what did I spend on restaurants last month?" |
| `GET .../{m}/transaction_categories` | GET | List transaction categories (with subcategories, colors, icons) | Reference data for transaction queries |
| `GET .../{m}/cashflow/distribution` | GET | Cashflow in/out distribution by category, date range | "What are my biggest expenses?" |
| `GET .../{m}/cashflow/available_money` | GET | Available money for a period | "How much do I have left this month?" |
| `GET .../{m}/cashflow/recurring_payments` | GET | Recurring payment detection (name, amount, frequency, merchant) | "What subscriptions am I paying for?" |
| `GET .../{m}/cashflow/daterange` | GET | Available date range for cashflow analysis | Context for date-bounded queries |
| `GET .../{m}/portfolio/insights` | GET | Portfolio profile and ranking | "How does my portfolio compare?" |
| `GET .../{m}/insights/financial_projection` | GET | Financial projection (params: duration, monthly_contribution) | "Where will I be in 10 years if I save X/month?" |

### Medium-Value Future Candidates

| Endpoint | HTTP Method | Description | Why Useful for LLM |
|----------|-------------|-------------|---------------------|
| `GET .../{m}/portfolio/geographical_diversification` | GET | Diversification score + analysis | Portfolio advice context |
| `GET .../{m}/portfolio/sector_diversification` | GET | Sector diversification score | Portfolio advice context |
| `GET .../{m}/portfolio/assets_leaderboard` | GET | Top/bottom performing assets | "What are my best/worst investments?" |
| `GET .../{m}/benchmarks/available_assets` | GET | Available benchmark indices for comparison | Context for performance questions |
| `GET .../cashflow/configuration` | GET | Cashflow configuration (tracked accounts, categories) | Context for cashflow queries |

### Low-Value / Internal

| Endpoint | HTTP Method | Description | Expose? |
|----------|-------------|-------------|---------|
| `GET /users/me/sync_status` | GET | Bank sync polling status | ⚠️ Internal — not useful for LLM |
| `GET /users/me/synchronizations` | GET | Bank connection sync states | ⚠️ Internal |
| `GET /users/me/subscription_details` | GET | Subscription info | ⚠️ Maybe — "am I a Plus subscriber?" |
| `GET /users/me/kyc_infos` | GET | KYC verification | ❌ Internal |
| `GET /users/me/notifications` | GET | Notification list | ⚠️ Low value |
| `GET /users/me/onboarding_tasks` | GET | Onboarding checklist | ❌ Internal |
| `GET /users/me/institution_provider_incidents` | GET | Bank connection incidents | ⚠️ Niche |
| `PUT /users/me/ui_configuration` | **PUT** | Update UI preferences | ❌ **MUTATING** — do not expose |
| `GET /bank_account_types` | GET | Reference: account type list | ⚠️ Reference data, low priority |
| `GET /companies` | GET | Company/institution search | ⚠️ Autocomplete, niche |
| `GET /holdings_account_contracts/finary` | GET | Finary investment contracts | ⚠️ Niche |
| `GET /invest/v2/me` | GET | Finary Invest account info | ⚠️ Only if user uses Finary Invest |
| `GET /invest/v2/ledgers/{uuid}` | GET | Finary Invest ledger details | ⚠️ Only if user uses Finary Invest |

---

## 3. Model Type Catalog

### Namespace: `FinaryExport.Models`

| Type | File | Purpose |
|------|------|---------|
| `FinaryResponse<T>` | `ApiEnvelope.cs` | API response envelope: `{ result, message, error }` |
| `FinaryError` | `ApiEnvelope.cs` | Error object: `{ code, message }` |
| `AssetCategory` (enum) | `AssetCategory.cs` | 10 values: Checkings, Savings, Investments, RealEstates, Cryptos, FondsEuro, Commodities, Credits, OtherAssets, Startups |

### Namespace: `FinaryExport.Models.User`

| Type | File | Purpose |
|------|------|---------|
| `FinaryProfile` | `FinaryProfile.cs` | Exportable profile: `(OrgId, MembershipId, ProfileName)` |
| `UserProfile` | `UserProfile.cs` | Current user: name, email, subscription, display currency |
| `UiConfiguration` | `UserProfile.cs` | Display preferences (currency) |
| `DisplayCurrencyInfo` | `UserProfile.cs` | Currency code + symbol |
| `Organization` | `Organization.cs` | Org with nested members |
| `OrganizationMember` | `Organization.cs` | Membership within org |
| `OrganizationUser` | `Organization.cs` | User within membership |
| `Membership` | `Membership.cs` | Simple org+membership ID pair |

### Namespace: `FinaryExport.Models.Accounts`

| Type | File | Purpose |
|------|------|---------|
| `Account` | `Account.cs` | Full account: balances, institution, ownership, positions |
| `AccountInstitution` | `Account.cs` | Bank/institution reference |
| `AccountCurrency` | `Account.cs` | Currency info |
| `AccountBankAccountType` | `Account.cs` | Account type classification |
| `HoldingsAccount` | `HoldingsAccount.cs` | Lighter account view (cross-category) |
| `OwnershipEntry` | `OwnershipEntry.cs` | Ownership share per membership |
| `OwnershipMembership` | `OwnershipEntry.cs` | Membership ref within ownership |
| `SecurityInfo` | `SecurityInfo.cs` | Security metadata: name, ISIN, price, expense ratio |
| `SecurityPosition` | `SecurityPosition.cs` | Position within an account: quantity, P&L |

### Namespace: `FinaryExport.Models.Portfolio`

| Type | File | Purpose |
|------|------|---------|
| `PortfolioSummary` | `PortfolioSummary.cs` | Portfolio overview: gross/net totals |
| `PortfolioValues` | `PortfolioSummary.cs` | Values section (total + assets/liabilities) |
| `PortfolioTotalValues` | `PortfolioSummary.cs` | Totals: amount, evolution, period performance |
| `TimeseriesData` | `TimeseriesData.cs` | Timeseries point: label, evolution, data array |
| `AllocationData` | `AllocationData.cs` | Allocation breakdown: total, distribution entries |
| `AllocationEntry` | `AllocationData.cs` | Single allocation item (recursive for sub-sectors) |
| `DividendSummary` | `DividendSummary.cs` | Dividend overview: annual income, yield, per-type breakdowns |
| `DividendEntry` | `DividendSummary.cs` | Individual dividend event |
| `NextYearEntry` | `DividendSummary.cs` | Projected dividend (date+value) |
| `DividendAssetInfo` | `DividendSummary.cs` | Asset reference within dividend |
| `FeeSummary` | `FeeSummary.cs` | Fee analysis: totals + timeseries |
| `FeeTotalValues` | `FeeSummary.cs` | Fee amounts/percentages (annual, cumulated, savings) |
| `AssetListEntry` | `AssetListEntry.cs` | Individual holding: name, value, P&L, category |

### Namespace: `FinaryExport.Models.Transactions`

| Type | File | Purpose |
|------|------|---------|
| `Transaction` | `Transaction.cs` | Full transaction: name, date, value, category, account |
| `TransactionCategory` | `TransactionCategory.cs` | Category with subcategories, colors, targets |
| `TransactionCurrency` | `Transaction.cs` | Currency ref |
| `TransactionInstitution` | `Transaction.cs` | Institution ref |
| `TransactionAccount` | `Transaction.cs` | Account ref within transaction |

---

## 4. Pagination Patterns

### Client-Side Auto-Pagination
The `FinaryApiClient` handles pagination internally via `GetPaginatedListAsync<T>`:
- Page-based: `?page={n}&per_page={pageSize}`
- Stops when `batch.Count < pageSize`
- Only used by `GetCategoryTransactionsAsync` (default pageSize=200)

### Other List Endpoints
All other list endpoints (`GetCategoryAccountsAsync`, `GetAssetListAsync`, `GetHoldingsAccountsAsync`, etc.) do **not** paginate — they return all results in a single call. The API returns full lists for these (typical counts: ~20 accounts, ~50 assets).

### MCP Implication
**MCP tools should NOT expose pagination parameters.** The client handles pagination transparently. The MCP tool gets the full list and returns it. This is correct for the current data volumes.

If the `transactions` endpoint from Section 2 (org-level search) is added later, it WOULD need pagination parameters exposed to the LLM for large result sets.

---

## 5. Context Requirements

### Organization Context (Critical)

Almost all data endpoints require `org_id` + `membership_id` in the URL path:
```
/organizations/{org_id}/memberships/{membership_id}/...
```

The `FinaryApiClient` stores these as internal state, set by:
1. `GetOrganizationContextAsync()` — auto-resolves owner's context
2. `SetOrganizationContext(orgId, membershipId)` — explicit switch

### MCP Bootstrap Sequence

An MCP session must follow this order:
1. **First:** Call `finary_get_org_context` or `finary_list_profiles` to discover available contexts
2. **Optionally:** Call `finary_set_profile` to switch to a non-default profile
3. **Then:** Any data tool

### UnifiedFinaryApiClient (Multi-Profile Aggregation)

The `UnifiedFinaryApiClient` decorator transparently aggregates across all memberships. For MCP:
- Could expose a `finary_set_unified_mode` tool, or
- Could make the MCP server decide based on context (simpler)
- The unified client handles all deduplication, ownership scaling, and caching internally

### Period Parameter

Many endpoints accept a `period` parameter:
- Valid values: `1d`, `1w`, `1m`, `3m`, `6m`, `1y`, `all`
- Default: `"all"` in most methods
- MCP tools should accept this as an optional string parameter with validation

### AssetCategory Enum

Category-parameterized endpoints accept one of 10 values:
```
checkings, savings, investments, real_estates, cryptos,
fonds_euro, commodities, credits, other_assets, startups
```

The enum maps to URL segments via `ToUrlSegment()`. MCP tools should accept the category as a string and validate against the enum.

**Transaction caveat:** Only `checkings`, `savings`, `investments`, `credits` support transactions (`HasTransactions()`). The MCP tool description should note this.

---

## 6. Safety Notes for Mutating Operations

### Current State: All Read-Only ✅

Every method on `IFinaryApiClient` is a GET request. There are **zero** mutating (POST/PUT/DELETE) operations in the client.

### Wire-Observed Mutations

Only one mutating endpoint was observed in the traffic capture:
- `PUT /users/me/ui_configuration` — updates display preferences (period mode)

**Recommendation:** Do NOT expose this as an MCP tool. It modifies user state with no undo. If write operations are added later, they should require explicit confirmation via MCP's tool confirmation protocol.

### Auth Operations (Out of Scope)

Clerk auth endpoints (POST sign_in, POST tokens, etc.) are handled by `ClerkAuthClient`, not `IFinaryApiClient`. These are infrastructure, not data operations — they should NOT be MCP tools.

---

## 7. Recommendations

### Phase 1: Core MCP Tools (15 tools)

All 15 methods from `IFinaryApiClient` should be exposed as MCP tools. They are all read-only, well-typed, and immediately useful to an LLM.

**Suggested tool list for Linus:**

```
finary_get_current_user          → "Who am I? What's my subscription?"
finary_list_profiles             → "What profiles/memberships do I have?"
finary_get_org_context           → Bootstrap: resolve default org context
finary_set_profile               → Switch active profile
finary_get_portfolio             → "What's my net worth?"
finary_get_portfolio_timeseries  → "How has my portfolio evolved?"
finary_get_dividends             → "How much dividend income do I earn?"
finary_get_geo_allocation        → "Where is my money geographically?"
finary_get_sector_allocation     → "What sectors am I invested in?"
finary_get_fees                  → "How much am I paying in fees?"
finary_get_category_accounts     → "Show me my checking accounts"
finary_get_category_timeseries   → "How have my investments performed?"
finary_get_category_transactions → "Show me my recent transactions"
finary_get_asset_list            → "What are my top holdings?"
finary_get_holdings_accounts     → "List all my accounts"
```

### Phase 2: High-Value Additions (requires new API client methods)

These need new methods added to `IFinaryApiClient`:

```
finary_search_transactions       → Org-level transaction search with filters
finary_get_cashflow_distribution → Spending breakdown by category
finary_get_recurring_payments    → Subscription/recurring payment detection
finary_get_financial_projection  → "Where will I be in N years?"
finary_get_cashflow_available    → "How much can I spend this month?"
```

### Phase 3: Niche/Reference

```
finary_get_diversification_score → Geo/sector diversification analysis
finary_get_assets_leaderboard    → Best/worst performers
finary_get_benchmarks            → Available benchmark indices
```

### MCP Server Architecture Notes

1. **Single `IFinaryApiClient` dependency** — the MCP server should take an `IFinaryApiClient` and expose each method as a tool. The `UnifiedFinaryApiClient` decorator can be swapped in for unified mode.
2. **JSON serialization** — all models use `SnakeCaseLower` naming policy. MCP responses should use the same convention for consistency.
3. **Error handling** — the `FinaryResponse<T>` envelope includes an `Error` property. MCP tools should surface error messages to the LLM.
4. **Rate limiting** — already handled at the HTTP layer (`RateLimiter` + `FinaryDelegatingHandler`). MCP server doesn't need its own rate limiting.

---

*Catalog complete. 15 implemented methods, 37 model types, 0 mutations. Trust the wire. — Livingston*
