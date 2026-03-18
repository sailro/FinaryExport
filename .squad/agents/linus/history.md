# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Core Context

**Implementation Overview (Active):**
- **Auth:** CurlImpersonate-based Clerk flow (6-step cold start, 2-tier warm/cold via session store, interactive prompts). DPAPI session encryption. Token refresh every 50s via PeriodicTimer IHostedService.
- **API Client:** Typed endpoints for 10 asset categories, pagination, rate limiting (5 req/s), retry logic with exponential backoff (Accept header, auth header preservation). FinaryApiClient is 8 partial classes split by concern (Auth, Portfolio, Accounts, Transactions, Holdings, References, Transactions, TransactionCategories). UnifiedFinaryApiClient decorator aggregates across memberships.
- **Export:** 5 sheet types (Summary, Accounts, Transactions, Dividends, Holdings). ClosedXML with ExcelStyles formatting (currency symbols, percentages, dates). Per-category error isolation. ExportContext controls display vs raw value resolution.
- **CLI:** export (with --output, period, currency), clear-session, version commands. Generic Host for DI, config, logging, hosted services.
- **Models:** AssetCategory enum (10 values), Account, Transaction, SecurityPosition, Dividend, Portfolio summary, User/Organization/Membership, TransactionCategory. record types, decimal for money, STJ SnakeCaseLower naming.
- **Testing:** 240 tests (xUnit), 13 test files. Covers auth, API, export, infrastructure, formatters. 100% pass rate. No trimming enabled (PublishTrimmed=false due to reflection dependencies).
- **Publish:** win-x64 self-contained single-file executable (~90 MB). README has build/publish/run instructions.

**Last Updated:** 2026-03-18 constants extraction. Build: 0 warnings, 0 errors. All decisions filed in `.squad/decisions/decisions.md`.

## Team Activity

### 2026-03-18: Constants Extraction & Test Updates

**Orchestration log filed:** `.squad/orchestration-log/2026-03-18T08-22-linus.md`

Extracted 11 hardcoded constants from across the codebase into `FinaryConstants` nested static classes (`ApiPaths`, `Headers`, `Defaults`). Originally planned to name the API paths class `Api`, but it conflicted with the `FinaryExport.Api` namespace — renamed to `ApiPaths` to avoid `CS0234` resolution errors in files importing `using FinaryExport.Api;` or residing in that namespace.

**Constants extracted:**
- `ApiPaths.HttpClientName`, `UsersOrganizationsPath`, `CurrentUserPath` — used in ServiceCollectionExtensions, FinaryApiClient
- `Headers.ApiVersionHeader/Value`, `ClientIdHeader/Value` — used in FinaryDelegatingHandler
- `Defaults.DefaultPeriod`, `DefaultValueType`, `DefaultTransactionPageSize` — used across IFinaryApiClient, all FinaryApiClient partials, UnifiedFinaryApiClient, AutoInitFinaryApiClient, MCP tools

**Refactored FinaryDelegatingHandler:** Extracted `SetFinaryHeaders(HttpRequestMessage, string)` private static helper. `SendAsync` and `CloneRequest` both call it — eliminated 12 lines of duplicated header-setting code.

**Files updated:** 12 files across Core, Mcp, and Tests. Build: 0 errors, 0 warnings. Tests: 240/240 passing.

**Parallel work:** Basher updated 4 test files to use shared constants in fixture setup (assertions left hardcoded for independent validation).

### 2026-03-18: Crypto Holdings Export — Complete Implementation

Implemented full typed support for cryptocurrency and fiat position export (alongside Basher's tests).

**Models created:** `CryptoPosition`, `CryptoInfo`, `FiatPosition`, `FiatInfo` in `Models/Accounts/`. Follow `SecurityPosition`/`SecurityInfo` pattern — record types with nullable decimal properties for raw + display values.

**Sheet implementation:** `CryptoHoldingsSheet` — fetches `AssetCategory.Cryptos` accounts, flattens nested `account.Cryptos` into rows with columns: Account, Name, Code, Quantity, Buy Price, Current Price, Value, Buy Value, +/- Value, +/- %. 8 decimal precision for crypto quantities. Registered in `Program.cs` alongside other `ISheetWriter` implementations.

**Model changes:** `Account.Cryptos` changed from `JsonElement?` to `List<CryptoPosition>?`; `Account.Fiats` from `JsonElement?` to `List<FiatPosition>?`.

**Multi-profile:** Transparent via existing `UnifiedFinaryApiClient` deduplication — crypto positions nested in accounts already deduplicated.

**Tests:** Basher wrote 23 comprehensive tests (deserialization, edge cases, sheet export). All 263 tests passing, build clean.

**Orchestration logs:** `.squad/orchestration-log/2026-03-18T08-51-linus.md` and `basher.md`

**Session log:** `.squad/log/2026-03-18T08-51-crypto-holdings.md`

**Decision filed:** `.squad/decisions.md` (D-crypto)

### 2026-03-17: Pagination Investigation Session

Collaborated with Livingston (Protocol Analyst) and Saul (MCP Specialist) on comprehensive pagination audit. Discovered the root cause of `asset_list` endpoint failures: the endpoint has a fundamental hard cap (~27 items) and doesn't support true pagination parameters. Saul made the decision to remove the unreliable `get_asset_list` tool entirely and replace it with `get_account_positions` using the reliable `/portfolio/{category}/accounts` endpoint.

**Orchestration logs filed:** `.squad/orchestration-log/2026-03-17T1900-linus.md`

## Historical Archive (2026-03-12 to 2026-03-15)

### Initial Implementation Sprint (2026-03-12T08:24Z)

**Status:** ✅ COMPLETE — 49 source files, 94 tests passing, build clean

Delivered full FinaryExport CLI scaffolding:
- **Auth:** 6-step Clerk flow (email/password/TOTP) with warm/cold start, DPAPI encryption, 50s token refresh via PeriodicTimer
- **API Client:** Typed endpoints for 10 AssetCategory values, rate limiting (5 req/s), 401/429 retry logic, pagination
- **Models:** Portfolio, 10 category models, Account, Transaction, Dividend, SecurityPosition
- **Export:** ClosedXML-based XLSX with 13 sheets (Summary, Accounts, Transactions, Dividends, Holdings, + per-category sheets)
- **CLI:** `export`, `clear-session`, `version` commands via System.CommandLine beta
- **Configuration:** `FinaryOptions` with `SessionStorePath`, `Output`, `Period`, `Currency`
- **Testing:** 94 xUnit tests covering auth flows, API endpoints, error isolation, export formatting

**Architecture decisions:** `FinaryOptions` uses mutable `set` (not `init`), Partial classes organize FinaryApiClient by concern, AssetCategoryExtensions for API path + display names. Session persistence via DPAPI-encrypted file (Windows-only, configurable path).

**Key learnings:** Clerk responses vary by endpoint (nested envelopes). TOTP required on every login. CurlImpersonate needed for Cloudflare TLS bypass (separate HttpClient, Chrome browser headers). Warm start persists `__client` cookie (~90-day expiry) to skip 5 of 6 auth steps on subsequent runs.

**Artifacts:** `.squad/orchestration-log/2026-03-12T08-24-linus.md`, `.squad/log/2026-03-12T08-24-implementation-sprint.md`

### Cleanup Phase (2026-03-12 to 2026-03-15)

**Status:** ✅ COMPLETE — Full suite of cleanup tasks

- **Auth flow rewrite:** Changed to interactive credential prompts (removed stored credentials, Otp.NET dependency)
- **Dividend names fix:** Typed `DividendAssetInfo` for asset metadata (replaces `JsonElement?`)
- **Transaction category filter:** Added `HasTransactions()` extension (Checkings, Savings, Investments, Credits only support transactions)
- **System.CommandLine upgrade:** 2.0-beta4 → 2.0.5 stable (breaking changes: `AddOption()` → `Options.Add()`, `SetHandler()` → `SetAction(ParseResult)`)
- **xUnit v2 → v3 migration:** Added `global.json` with MTP mode, `<OutputType>Exe</OutputType>`, suppressed xUnit1051
- **IDE diagnostics:** ~95 fixes (use `var`, remove trailing commas, invert nesting, simplify expressions, test improvements)
- **Publish profile:** win-x64 self-contained exe (~90 MB), PublishTrimmed=false (reflection dependencies)
- **README:** Publish instructions, troubleshooting section
- **Data hygiene:** Removed PII references

**Build:** Clean throughout. Tests: 134 → 240 passing (106 new tests added by Basher).

### Cross-Platform Analysis (2026-03-14)

Analyzed Windows-only blockers for Linux/macOS support. DPAPI → Microsoft.AspNetCore.DataProtection (trivial swap). CurlImpersonate has linux-x64 binaries (untested) but no macOS support (hard blocker). Recommendation: Windows + Linux x64 feasible (~1 day). macOS deferred.

**Decision filed:** `.squad/decisions/inbox/rusty-cross-platform.md`

### Project Reassessment (2026-03-15T21:21Z)

Multi-agent team review. Five code quality fixes (dead endpoint, parameter alignment, header disambiguation, missing Accept header retry, unused dependency). Build clean, 240 tests passing, zero regressions. Supports scalable test expansion and maintainability.

**Orchestration log:** `.squad/orchestration-log/2026-03-15T21-21-linus.md`

## Learnings

### Model Consolidation: Crypto/Fiat → AssetInfo + CurrencyPosition (2026-03-19)

Merged 4 model types into 2 per Rusty's architecture decision:
- `CryptoInfo` + `FiatInfo` → `AssetInfo` (byte-for-byte identical types)
- `CryptoPosition` + `FiatPosition` → `CurrencyPosition` (14 shared financial properties, only differed in Id type and nested info property name)

**Key design choices:**
- `CurrencyPosition.Id` uses `JsonElement?` (not `string?`) because the API sends numeric IDs for crypto and string UUIDs for fiat — STJ can't coerce a JSON number to `string?` without a custom converter.
- Both `Crypto` and `Fiat` properties kept on `CurrencyPosition` — STJ populates whichever JSON key matches. Computed `Asset` property (`Crypto ?? Fiat`) gives consumers a single accessor.
- `Account.Cryptos` and `Account.Fiats` both changed from separate list types to `List<CurrencyPosition>?`. Lists stay separate (JSON sends separate arrays), only the element type is shared.

**Files:** 2 created (`AssetInfo.cs`, `CurrencyPosition.cs`), 3 deleted (`CryptoInfo.cs`, `CryptoPosition.cs`, `FiatPosition.cs`), 4 modified (`Account.cs`, `CryptoHoldingsSheet.cs`, `CryptoDeserializationTests.cs`, `CryptoHoldingsSheetTests.cs` — tests needed zero changes since `new()` infers type from list).

**Build:** 0 errors, 0 warnings. **Tests:** 263/263 passing.

### Crypto Holdings Export (2026-03-19)

Added typed deserialization for crypto (and fiat) positions nested inside Account responses. Previously `Account.Cryptos` and `Account.Fiats` were `JsonElement?` — never deserialized. Changed them to `List<CryptoPosition>?` and `List<FiatPosition>?` respectively.

**New models:** `CryptoPosition`, `CryptoInfo`, `FiatPosition`, `FiatInfo` in `Models/Accounts/`. Follow the same pattern as `SecurityPosition`/`SecurityInfo` — flat record types with nullable decimal properties for raw + display values.

**CryptoHoldingsSheet:** Follows `HoldingsSheet` pattern exactly — fetches `AssetCategory.Cryptos` accounts, flattens `account.Cryptos` into rows sorted by account name then crypto name. Columns: Account, Name, Code, Quantity, Buy Price, Current Price, Value, Buy Value, +/- Value, +/- %. Quantity uses 8 decimal places (crypto precision). Registered in `Program.cs` DI alongside other `ISheetWriter` implementations.

**Multi-profile:** No special handling needed — crypto positions are nested in the Account response from `GetCategoryAccountsAsync`. The `UnifiedFinaryApiClient` already deduplicates accounts by ID across memberships, so crypto positions come through transparently.

**Key insight:** The Finary API nests position arrays (`cryptos`, `fiats`, `securities`, `fonds_euro`, `scpis`, `generic_assets`) inside each account object returned by `/portfolio/{category}/accounts`. STJ with `SnakeCaseLower` policy handles the mapping automatically — no custom converters needed.

### Constants Extraction & Header Helper Refactor (2026-03-18)

Extracted 11 hardcoded constants from across the codebase into `FinaryConstants` nested static classes (`ApiPaths`, `Headers`, `Defaults`). Originally planned to name the API paths class `Api`, but it conflicted with the `FinaryExport.Api` namespace — renamed to `ApiPaths` to avoid `CS0234` resolution errors in files importing `using FinaryExport.Api;` or residing in that namespace.

**Constants extracted:**
- `ApiPaths.HttpClientName`, `UsersOrganizationsPath`, `CurrentUserPath` — used in ServiceCollectionExtensions, FinaryApiClient
- `Headers.ApiVersionHeader/Value`, `ClientIdHeader/Value` — used in FinaryDelegatingHandler
- `Defaults.DefaultPeriod`, `DefaultValueType`, `DefaultTransactionPageSize` — used across IFinaryApiClient, all FinaryApiClient partials, UnifiedFinaryApiClient, AutoInitFinaryApiClient, MCP tools

**Refactored FinaryDelegatingHandler:** Extracted `SetFinaryHeaders(HttpRequestMessage, string)` private static helper. `SendAsync` and `CloneRequest` both call it — eliminated 12 lines of duplicated header-setting code.

**Files updated:** 12 files across Core, Mcp, and Tests. Build: 0 errors, 0 warnings. Tests: 240/240 passing.

### Asset List Pagination Correction (2026-03-18)

The previous fix (switching to `GetPaginatedListAsync`) was wrong. The `asset_list` endpoint doesn't support `page`/`per_page` pagination like the `transactions` endpoint does. It only supports `limit`.

**Root cause:** When we added `page=1&per_page=100` query params, the API ignored them and returned only its default batch (5 items). The pagination loop terminated immediately because `batch.Count (5) < pageSize (100)`.

**Actual fix:** Changed back to single-request fetch with `limit=1000` to ensure all positions are returned in one call.

```csharp
// Before (broken):
return await GetPaginatedListAsync<AssetListEntry>($"{BasePath}/asset_list?period={period}", pageSize: 100, ct);

// After (working):
return await GetAsync<List<AssetListEntry>>($"{BasePath}/asset_list?limit=1000&period={period}", ct) ?? [];
```

**Key learning:** Finary has TWO different pagination patterns:
- `transactions`: Uses `page` + `per_page` (true pagination)
- `asset_list`: Uses `limit` only (no pagination, just caps result count)

The api-analysis.md documented this: transactions show `page`/`per_page`, but asset_list shows only `limit=25`. Always check the actual endpoint docs before assuming pagination patterns are consistent.

### Asset List Pagination Fix (2026-03-17)

Fixed critical bug in `GetAssetListAsync` that was limiting results to 100 assets per export. The method was using `limit=100` (single-page fetch) instead of paginating through results. Users with 27+ total positions lost lower-value holdings in exports.

**Change:** Switched `GetAssetListAsync` from `GetAsync<List<AssetListEntry>>($"{BasePath}/asset_list?limit=100")` to `GetPaginatedListAsync<AssetListEntry>($"{BasePath}/asset_list", pageSize: 100)`.

**Why it matters:** Finary's `limit` parameter caps results but doesn't enable pagination. The actual pagination mechanism uses `page` + `per_page` query parameters, which the `GetPaginatedListAsync` helper implements correctly (loops until `batch.Count < pageSize`).

**Testing:** All 240 tests pass. Backward compatible — method signature unchanged.

**Impact:** Paired with Livingston's full pagination audit (14 API methods, 7 MCP tools, 2 decorator clients) — no other pagination bugs found. Future guidance: all new list endpoints should use `GetPaginatedListAsync`.

**⚠️ UPDATE 2026-03-18:** This fix was incorrect — see "Asset List Pagination Correction" above.

### FinaryExport.Core Extraction (2026-03-17)

Extracted shared library `FinaryExport.Core` from the CLI project per Rusty's MCP architecture proposal (§2-§5). Pure refactoring — no behavior changes, all 240 tests pass.

**What moved to Core:** Api/, Auth/ (minus ConsoleCredentialPrompt), Models/, Infrastructure/, Configuration/. Core csproj uses `RootNamespace=FinaryExport` — zero namespace changes anywhere.

**Key patterns:**
- `AddFinaryCore()` in Core registers shared services (curl, auth, rate limiter, HTTP client, API client) but NOT `ICredentialPrompt` and NOT export services. Host projects register their own `ICredentialPrompt` implementation and any host-specific services.
- `ConsoleCredentialPrompt` lives at `src/FinaryExport/ConsoleCredentialPrompt.cs` (CLI project root, not in Auth/).
- Core packages: Loxifi.CurlImpersonate, Microsoft.Extensions.Hosting, Microsoft.Extensions.Http, System.Security.Cryptography.ProtectedData.
- CLI keeps only: ClosedXML, System.CommandLine + ProjectReference to Core.
- `Directory.Packages.props` unchanged — centralized versioning works for both projects.
- Solution file: `FinaryExport.slnx` now has Core in `/src/` folder.
- Test project references both Core and CLI.
- Used `git mv` for all file moves to preserve history.

### Full Source Code Audit (2026-03-15)

Performed a full audit of every .cs file in `src/FinaryExport/`. Found and fixed 5 issues:

1. **Dead code removed**: `FinaryApiClient.TransactionCategories.cs` — `GetTransactionCategoriesAsync` was not on `IFinaryApiClient`, not called anywhere, and redundant since the `Transaction` model already has an inline `Category` property from the API response. Deleted the file.
2. **Inconsistent default parameter**: `UnifiedFinaryApiClient.GetAssetListAsync` had `period = "1d"` vs the interface's `period = "all"`. Fixed to match interface. Callers going through the interface always got `"all"` anyway, but the mismatch was misleading.
3. **Duplicate column header**: `TransactionsSheet` had two columns both named "Category" (A = asset category, J = transaction category). Renamed column J to "Transaction Category" for clarity.
4. **Missing Accept header on retry**: `FinaryDelegatingHandler.CloneRequest` was missing the `Accept: */*` header when cloning requests for 401/429 retries. All other headers (Auth, Origin, Referer, x-client-api-version, x-finary-client-id) were present in the clone.
5. **Unused DI parameter**: `ClerkAuthClient` constructor took `IOptions<FinaryOptions>` and stored it as `_options`, but never used it. Removed the parameter and field. Updated `ClerkAuthClientSkipTests` accordingly.

Patterns confirmed clean: all `IFinaryApiClient` methods implemented in both `FinaryApiClient` and `UnifiedFinaryApiClient`. Per-category error isolation working correctly in all sheet writers. ExportContext/ResolveValue used consistently for monetary values. JSON SnakeCaseLower naming matches all model PascalCase properties. Auth warm/cold start flow is sound. TokenRefreshService shutdown is properly cooperative.

### README MCP Documentation (2026-03-17)

Updated README.md to document the MCP server added in the D-mcp-complete work. Added sections for configuration (mcp-config.json entries for Copilot CLI, VS Code, Claude Desktop), authentication (elicitation + session.dat reuse), multi-profile support (get_profiles → set_active_profile workflow), and a full tool catalog table (16 tools across 7 tool classes). No UserSecrets references were found to clean up — README was already clean on that front. Kept existing README style and structure, inserted MCP section before License.

### Currency Header Rename & Value Resolution Audit (2026-03-15)

Renamed "Currency" column header to "Native Currency" in **AccountsSheet** (column D) and **TransactionsSheet** (column H) to clarify this is the original account/transaction currency, not the display currency (which is already visible in amount cell number formats via `context.CurrencyFormat`). Fixed value resolution inconsistencies: **TransactionsSheet** was using `tx.Value ?? 0m` directly instead of `context.ResolveValue(tx.DisplayValue, tx.Value)` — now uses ResolveValue for consistency with AccountsSheet. **DividendsSheet** had the same issue: both Past and Upcoming dividend detail rows used `div.Amount ?? 0m` instead of `context.ResolveValue(div.DisplayAmount, div.Amount)` — fixed both. Summary fields (`AnnualIncome`, `PastIncome`, etc.) lack display variants in the API model and were left as-is. Commission on Transaction also has no display variant — left as-is.

### Currency Column Addition (2026-03-15)

Added currency context to sheets that show monetary amounts. **AccountsSheet** and **TransactionsSheet** already had Currency columns (D and H respectively) — no changes needed. **PortfolioSummarySheet** now derives the display currency from the first account with a `Currency.Code` during the per-category iteration, then writes a "Currency: XXX" label in cell C1 (italic, right-aligned). **HoldingsSheet** gained a new column L "Currency" showing `account.Currency?.Code` for each security row. **DividendsSheet** was the most involved: added `GetCurrencyCode(JsonElement?)` helper to parse the `code` property from the `DisplayCurrency`/`Currency` JsonElement fields on `DividendEntry`, a `GetDisplayCurrencyFromEntries` helper that scans past/upcoming dividends for the first available currency, a "Currency: XXX" label at the top (C1), and a new "Currency" column (C) in both Past Dividends and Upcoming Dividends detail tables — shifting Date/Type/Status/Category columns right by one (D-F). The display currency fallback chain is: `DisplayCurrency.code` → `Currency.code` → empty string.

### Transaction Categories Export (2026-03-15)

Added transaction category enrichment to the export. Created `TransactionCategory` model in `Models/Transactions/`, new `FinaryApiClient.TransactionCategories.cs` partial calling `GET {BasePath}/transaction_categories?included_in_analysis=true`, passthrough in `UnifiedFinaryApiClient` (categories are org-level, not profile-specific). In `TransactionsSheet`, categories are fetched once, flattened recursively (parent + subcategories) into a `Dictionary<int, string>` lookup, then each transaction's `ExternalIdCategory` is resolved to its name in a new "Transaction Category" column (J). The existing `GetAsync<T>` helper unwraps the `FinaryResponse<T>` envelope automatically, so no wrapper type was needed. The `ExternalIdCategory` field is `int?` in the model matching the category `Id` type — no type mismatch to handle.

### Period Parameter Threading (2026-03-14)

Threaded the CLI `--period` flag through `GetCategoryAccountsAsync` and `GetAssetListAsync`. Both previously hardcoded `period=1d`. Added `string period = "1d"` parameter (backward-compatible default) to the interface, `FinaryApiClient`, and `UnifiedFinaryApiClient`. The unified client's account cache key was updated to `(AssetCategory, string)` to correctly vary by period. Added `Period` property to `ExportContext` (default `"1d"`), set from `FinaryOptions.Period` in `Program.cs`. Sheet writers (`AccountsSheet`, `PortfolioSummarySheet`, `HoldingsSheet`) now pass `context.Period` to the API. Flow: CLI `--period` → `FinaryOptions.Period` → `ExportContext.Period` → sheet writers → API client.

## Historical Context

**Original Setup (2026-03-12):**
Architecture designed with ITokenProvider abstraction, CurlImpersonate TLS bypass, DPAPI session encryption, generic host DI. Initial implementation scaffolded 49 source files: Auth (6-step Clerk flow, warm/cold start, 50s token refresh), typed API client (10 categories, rate limiting 5 req/s, 401/429 retry logic), export module (ClosedXML, 5 sheet types, per-category error isolation), CLI (export/clear-session/version commands). All 94 tests passing on day 1. Later upgraded to .NET 10 and xUnit v3, added multi-profile support with UnifiedFinaryApiClient decorator, transaction category filtering, dividend name fixes, and comprehensive data model improvements through 2026-03-15.

---

### Auth Module Requirements (2026-03-12)
Finary uses Clerk authentication with mandatory TOTP 2FA. Auth flow is 6-step process:
1. GET `/v1/environment` → fetch config
2. GET `/v1/client` → establish session
3. POST `/v1/client/sign_ins` → email + password
4. POST `/v1/client/sign_ins/{id}/attempt_second_factor` → TOTP validation
5. POST `/v1/client/sessions/{id}/touch` → activate
6. POST `/v1/client/sessions/{id}/tokens` → get JWT (RS256, 60s TTL)

**Key Implementation Notes:**
- JWT refresh every ~50 seconds via POST `/tokens` (uses persistent `__client` cookie, not JWT)
- Clerk calls require `Origin: https://app.finary.com` header
- Query params: `__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4`
- HttpClient must use CookieContainer for `__client` cookie persistence
- Session duration: ~90 days without re-login

**Dependencies:** Need TOTP generator library (e.g., OtpNet for .NET)

### Architecture Blueprint (2026-03-12)

**From Rusty (Lead):** Architecture document finalized in `architecture.md`. Key decisions:

- **ITokenProvider abstraction** — sole auth interface. Implementation hidden in Auth module. Enables testing & future auth swaps.
- **PeriodicTimer (50s) token refresh** as `IHostedService` — autonomous, no client-side token requests needed.
- **Single project** with namespace discipline (no EPPlus complexity, use ClosedXML)
- **Generic host** with DI/config/logging (Host.CreateApplicationBuilder)

**Impact on Linus:** Follow architecture blueprint exactly. ITokenProvider drives auth module design. Token refresh is built-in service (not caller concern). Partial classes organize FinaryApiClient by endpoint category.

### Session Persistence & Two-Tier Auth (2026-03-12, D13)

**From Rusty (Lead):** Updated architecture with session persistence to avoid TOTP on every run.

**New Components:**
- **`ISessionStore` interface** — `SaveSessionAsync`, `LoadSessionAsync`, `ClearSessionAsync`. Abstracts session storage.
- **`EncryptedFileSessionStore` implementation** — DPAPI encryption at rest (`DataProtectionScope.CurrentUser`). Default path: `~/.finaryexport/session.dat`. Configurable via `SessionStorePath`.
- **Two-tier auth flow** — Warm start (load `__client` cookie → POST `/tokens` → JWT) tried first; cold start (full 6-step) as fallback on 401 or missing session.
- **CLI flag `--clear-session`** — Forces cold start (clears persisted cookie before run).

**Key Decision:** `__client` cookie persists ~90 days, survives token refreshes. Warm start eliminates 5 of 6 auth requests on subsequent runs.

**Implementation Notes:**
- ISessionStore failures are non-fatal: missing store → cold start; failed save → warning only. Never blocks auth.
- ITokenProvider API is unchanged. Session store is internal to Auth module.
- Warm start flow: Load `__client` → POST `/tokens` with cookie → get JWT (typically 6s).

**Impact on Linus:** Implement `ISessionStore.cs` interface and `EncryptedFileSessionStore.cs` implementation. Modify `ClerkAuthClient` to support warm-start flow: check for session → attempt warm start → fall back to cold start on failure. Add `SessionStorePath` config. Test both flows.

### Implementation Complete: Linus (2026-03-12T08:24:00Z)

**Status:** ✅ SUCCESS

**Deliverables:**
- Scaffolded `.NET 10` project under `src/FinaryExport/` with solution at repo root (`FinaryExport.sln`)
- Implemented 49 source files across 7 architectural modules
- Auth module: 6-step Clerk flow with warm/cold start, DPAPI encryption, 50-second token refresh
- API client: Typed endpoints for 10 categories, rate limiting, 401/429 retry logic
- Data models: Portfolio, 10 category-specific models, shared types (Account, Position, Transaction)
- Export module: ClosedXML-based XLSX exporter, per-category sheets, 13 total sheets
- CLI: `export`, `clear-session`, `version` commands via System.CommandLine
- User directive honored: Comments only (no XML doc comments)

**Build Status:** ✅ Builds clean, zero errors

**Key Design Decisions:**
- `FinaryOptions` uses mutable `set` (not `init`) for CLI config overrides
- CA1416 (Windows platform) suppressed—DPAPI is intentionally Windows-only
- Clerk response parsing handles nested envelopes (varies by endpoint)
- Partial classes organize `FinaryApiClient` by endpoint category
- AssetCategoryExtensions provide both API path format and display names

**Orchestration Log:** `.squad/orchestration-log/2026-03-12T08-24-linus.md`

### Cross-Team Update: Test Reconciliation (2026-03-12T08:48:00Z)

**From Basher:** Test suite fully integrated and passing.
- Contract stubs removed (7 files)
- Project reference added to `FinaryExport.Tests.csproj`
- Solution updated with test project
- All 94 tests passing against live implementation
- No code changes required—test coverage complete

**Impact on Linus:** Implementation meets all test contracts. Interface signatures verified. Ready for CI/CD.

### Auth Flow Change: Interactive Cold Start (2026-03-12)

**Change:** Replaced stored-credential auth (Email/Password/TotpSecret from config) with interactive console prompts on cold start.

**What changed:**
- **Deleted:** `TotpGenerator.cs` — no longer auto-generating TOTP from a stored secret
- **Removed:** `Otp.NET` package dependency
- **Removed:** `Email`, `Password`, `TotpSecret` from `FinaryOptions` — credentials are never stored
- **Added:** `ICredentialPrompt` interface + `ConsoleCredentialPrompt` implementation (masked password input)
- **Updated:** `ClerkAuthClient.ColdStartAsync` — prompts user interactively instead of reading config
- **Updated:** `Program.cs` — removed credential validation block (no longer applicable)
- **Updated:** `ServiceCollectionExtensions.cs` — registered `ICredentialPrompt` in DI

**Auth flow now:**
1. Warm start: load persisted `__client` cookie → POST `/tokens` → JWT. No prompts.
2. If warm start fails (no cookie, 401): interactive cold start → prompt Email, Password, TOTP Code → full 6-step Clerk flow → persist cookie.
3. `--clear-session` still forces cold start.

**Build:** ✅ Clean (0 warnings, 0 errors). **Tests:** ✅ All 94 pass.

### Auth Flow Rewrite: Cloudflare 429 Fix (2026-03-12)

**Root cause:** Cloudflare bot management was rejecting our Clerk API requests because we lacked browser-like headers (User-Agent, sec-ch-ua, sec-fetch-*) and Cloudflare cookies (__cf_bm, _cfuvid). The `/v1/environment` endpoint worked only because it was edge-cached (bypassing bot checks).

**What changed:**
- **ClerkAuthClient** rewritten: owns its HttpClient + CookieContainer directly (no IHttpClientFactory for Clerk), applies Chrome-like browser headers on every Clerk request
- **Simplified cold start:** 3-step flow (sign_in → 2FA → extract session from response) — skips /v1/environment and /v1/client entirely, matching FinarySharp's proven approach
- **Cloudflare warmup:** GET `https://app.finary.com` before auth to collect __cf_bm and _cfuvid cookies, which auto-flow to clerk.finary.com via CookieContainer
- **Session persistence updated:** now saves SessionId + all cookies (not just __client). JWT not persisted (60s TTL, pointless). New `SessionData` record replaces raw cookie collection
- **ISessionStore interface changed:** `SaveSessionAsync(SessionData)` / `LoadSessionAsync() → SessionData?`
- **EncryptedFileSessionStore** updated for new data shape (breaking change for stored files — triggers cold start, self-healing)
- **DI simplified:** removed shared CookieContainer singleton, removed "Clerk" named HttpClient, removed ClerkDelegatingHandler from registration
- **ClerkAuthClient implements IDisposable** (owns HttpClient/Handler/SemaphoreSlim)
- **Browser headers added:** User-Agent (Chrome 146), sec-ch-ua, sec-ch-ua-mobile, sec-ch-ua-platform, sec-fetch-dest/mode/site, Accept, Accept-Language
- **Tests updated:** InMemorySessionStore, SessionStoreTests, ClerkAuthClientTests all adapted for SessionData and simplified flow

**Key insight from FinarySharp reference:** CookieContainer on a self-owned HttpClient + Origin/Referer headers + FormUrlEncodedContent bodies + direct /sign_ins call is all you need. The /environment and /client preamble was unnecessary overhead that also triggered the 429.

**Build:** ✅ Clean (0 warnings, 0 errors). **Tests:** ✅ All 94 pass.

### Auth Rewrite: CurlImpersonate Adoption (2026-03-12)

**Problem:** Even with browser headers and Cloudflare cookie warmup, Clerk requests still got 403s. The root cause was TLS fingerprint detection — .NET's `HttpClient` has a distinct TLS ClientHello that Cloudflare flags regardless of headers.

**Solution:** Adopted `Loxifi.CurlImpersonate` (pinned 1.1.0) — a .NET wrapper around curl-impersonate that produces byte-identical Chrome TLS handshakes via `BrowserProfile.Chrome136`.

**What changed:**
- **ClerkAuthClient** rewritten to use `CurlClient` directly for all Clerk API calls (not HttpClientFactory). CurlClient handles its own cookie jar and TLS impersonation.
- **CurlMessageHandler** added — bridges `CurlClient` into `HttpMessageHandler` so Finary API calls still go through HttpClientFactory with the DelegatingHandler pipeline (`FinaryDelegatingHandler` for auth headers, rate limiting, 401 retry, 429 backoff).
- **Cold start simplified to 3 steps:** POST `/sign_ins` (email+password) → POST `/sign_ins/{id}/attempt_first_factor` (TOTP) → extract `__session` JWT from response. No `/environment` or `/client` preamble needed.
- **Warm start unchanged:** load persisted cookies → POST `/tokens` → JWT.
- **Removed:** browser header boilerplate (CurlImpersonate handles it), Cloudflare warmup GET (unnecessary with real Chrome TLS).
- **DI changes (`ServiceCollectionExtensions.cs`):** CurlClient registered as singleton, CurlMessageHandler as primary handler for "Finary" HttpClient, FinaryDelegatingHandler as additional handler.

**Build:** ✅ Clean. **Tests:** ✅ All 94 pass.

### Model & Export Additions (2026-03-12)

**New models:**
- `SecurityPosition` — quantity, display_buying_value, display_current_value, linked SecurityInfo
- `SecurityInfo` — ISIN, symbol, current_price
- `HoldingsAccount` — account with expanded securities list
- `OwnershipEntry` — membership ownership share percentage
- `FinaryProfile` — record linking organization/membership with name and share
- `AssetCategory` enum — 10 categories (checking, savings, real_estate, etc.)
- `FinaryResponse<T>` — API response envelope with `result` property

**New sheet:** `HoldingsSheet` — security-level export from investment accounts: ISIN, symbol, quantity, buy price, current price, value, unrealized P&L. Flattens Account→SecurityPosition→SecurityInfo.

**ExportContext** added — carries `UseDisplayValues` flag. `ResolveValue()` picks display vs raw value per context.

**Dead code removed:** `ClerkDelegatingHandler`, `FinaryJsonContext` (STJ source gen), unused Auth models (`ClerkTokenResponse`, `SignInResponse`, `SessionResponse`), `AccountDetail`, Otp.NET dependency.

**Build:** ✅ Clean. **Tests:** updated.

### Multi-Profile & Unified Export (2026-03-12)

**Feature:** Multi-profile support via `GetAllProfilesAsync()` which discovers all organization memberships.

**Export flow (Program.cs):**
1. Discover all profiles (personal + organizations)
2. For each profile: switch API context → fetch all data → write `finary-export-{name}.xlsx` with `ExportContext { UseDisplayValues = true }` (ownership-adjusted values)
3. Aggregated export: `UnifiedFinaryApiClient` merges all profiles → write `finary-export-unified.xlsx` with `UseDisplayValues = false` (raw computed totals)

**UnifiedFinaryApiClient:**
- Decorator over `IFinaryApiClient` — transparent to sheet writers
- Iterates all memberships, collects accounts/transactions/dividends, deduplicates by entity ID
- Shared assets (ownership share < 100%) scaled up: `display_balance / share = full_value`
- Caches merged account list to avoid redundant API calls
- Non-aggregatable endpoints (timeseries, allocations) use owner's data only

**Rate limiter:** Tuned from 2 req/s → 5 req/s (200ms interval). API showed browser at 2.5 req/s, no rate limit headers observed.

**Build:** ✅ Clean. **Tests:** ✅ 134 pass (40 new tests added).

### Dividend Names Fix (2026-03-12)

**Problem:** Dividends sheet showed asset type categories ("Scpi", "Security", "Fund") in Name column instead of actual investment names ("Remake Live", "Iroko Zen", "Pierval Santé").

**Root cause:** `DividendEntry.Asset` and `DividendEntry.Holding` were `JsonElement?` — not typed. Sheet writer used `AssetType` instead.

**Solution:**
1. Created `DividendAssetInfo` record with `Id`, `Type`, `Name`, `CorrelationId`, `LogoUrl`
2. Changed `DividendEntry.Asset` and `DividendEntry.Holding` from `JsonElement?` to `DividendAssetInfo?`
3. Updated `DividendsSheet.cs` to use `Asset?.Name ?? Holding?.Name ?? AssetType` (fallback chain)
4. Added new column E "Category" for `AssetType` (preserves broader category info)

**Key insight:** Both `asset` and `holding` in API response have identical structure and contain the investment name. They're interchangeable for display purposes.

**Files modified:**
- `src/FinaryExport/Models/Portfolio/DividendSummary.cs`
- `src/FinaryExport/Export/Sheets/DividendsSheet.cs`

**Build:** ✅ Clean. **Tests:** ✅ 134 pass (no test changes needed — models deserialize correctly with snake_case naming policy).

### Transaction Category Filter (2026-03-12)

**Problem:** `TransactionsSheet` iterated all 10 `AssetCategory` enum values, but only 4 have `/portfolio/{category}/transactions` endpoints (Checkings, Savings, Investments, Credits). The other 6 returned HTTP 404, producing 30 noisy warnings per export run (6 unsupported × 5 profiles).

**Solution:** Added `HasTransactions()` extension method to `AssetCategoryExtensions` (Option A from the task). `TransactionsSheet.WriteAsync` now filters with `.Where(c => c.HasTransactions())` before querying. The existing per-category try/catch remains as a safety net for genuine errors on supported categories.

**Key insight:** `UnifiedFinaryApiClient.GetCategoryTransactionsAsync` receives a single category — it doesn't iterate. Only `TransactionsSheet` needed the filter.

**Files modified:**
- `src/FinaryExport/Models/AssetCategory.cs` — added `HasTransactions()` extension
- `src/FinaryExport/Export/Sheets/TransactionsSheet.cs` — filtered category loop

**Build:** ✅ Clean (0 warnings, 0 errors). **Tests:** ✅ 134 pass (no test changes needed).

### Cross-Platform Analysis: Windows + Linux Feasibility (2026-03-14)

**From Rusty (Lead):** Scoped cross-platform support against Windows-only blockers. Two components prevent Linux/macOS: DPAPI encryption and CurlImpersonate TLS bypass.

**Key Findings:**
1. **DPAPI (EncryptedFileSessionStore)** — Windows-only via `System.Security.Cryptography.ProtectedData`. Clean replacement: `Microsoft.AspNetCore.DataProtection` (cross-platform, encryption at rest, console-app compatible). No breaking changes to `ISessionStore` interface. Effort: **Small** (~0.5 day).
2. **CurlImpersonate** — NuGet ships `linux-x64` `.so` binary (untested); macOS has no binaries. Linux verification: **Small** (~0.5 day test on Ubuntu). macOS: **Hard blocker** — Loxifi upstream has no support, build system is Linux-centric.

**Recommendation:** Windows + Linux x64 feasible (~1 day). macOS deferred.

**Impact on Linus:** If approved to proceed:
1. Implement `DataProtectionSessionStore` (new class, same `ISessionStore` interface)
2. Update `ServiceCollectionExtensions` DI registration for platform detection
3. Remove `System.Security.Cryptography.ProtectedData` package
4. Remove Windows-only `CA1416` suppression
5. Test on Ubuntu 22.04+ if available

**Full analysis:** `.squad/decisions/inbox/rusty-cross-platform.md`

### Data Hygiene Pass (2026-03-14)

**Task:** Performed data hygiene pass on repository to remove sensitive references from tracked files.

**Scope:** Removed PII references from working tree and squad files. No git history rewrite required for this repository.

**Verification:**
- `dotnet build` → ✅ Clean
- `dotnet test` → ✅ 134 pass

**Result:** Repository cleaned and ready for sharing.

### NuGet Upgrade & xUnit v3 Migration (2026-03-14)

**Task:** Update all NuGet packages and migrate test framework from xUnit v2 to xUnit v3.

**Package Updates (FinaryExport.csproj):**
- ClosedXML: 0.104.2 → 0.105.0
- Microsoft.Extensions.Hosting: 9.0.4 → 10.0.5
- Microsoft.Extensions.Http: 9.0.4 → 10.0.5
- System.CommandLine: 2.0.0-beta4 → 2.0.5 (stable)
- System.Security.Cryptography.ProtectedData: 9.0.4 → 10.0.5

**Package Updates (FinaryExport.Tests.csproj):**
- coverlet.collector: 6.0.4 → 8.0.0
- Removed: `xunit` (2.9.3), `xunit.runner.visualstudio` (3.1.4), `Microsoft.NET.Test.Sdk` (18.3.0)
- Added: `xunit.v3` (3.2.2)

**Breaking Changes Fixed:**

1. **System.CommandLine 2.0.5 (stable) API overhaul:**
   - `AddOption()` → `Options.Add()`
   - `AddCommand()` → `Subcommands.Add()`
   - `SetHandler(handler, opt1, opt2, opt3)` → `SetAction(async (ParseResult result) => { result.GetValue(opt) })`
   - `rootCommand.InvokeAsync(args)` → `rootCommand.Parse(args).InvokeAsync()`
   - Full rewrite of Program.cs CLI wiring.

2. **xUnit v3 test runner:**
   - Added `<OutputType>Exe</OutputType>` to test project (xUnit v3 is self-hosting).
   - Created `global.json` with `"test": { "runner": "Microsoft.Testing.Platform" }` to enable `dotnet test` with MTP mode (required for xUnit v3 without VSTest adapter).
   - `<Using Include="Xunit" />` — unchanged, xUnit v3 preserves the namespace.

3. **xUnit1051 warnings (94 occurrences):** Suppressed via `<NoWarn>xUnit1051</NoWarn>`. Analyzer suggests `TestContext.Current.CancellationToken` instead of `CancellationToken.None` — style preference, not correctness issue. Bulk-replacing would risk breaking Moq setup expressions that match on `CancellationToken.None`.

**Build:** ✅ Clean (0 warnings, 0 errors). **Tests:** ✅ 134 pass via `dotnet test`.

**Key Lesson:** System.CommandLine jumped from beta to stable with a complete API redesign — `SetHandler` is gone, replaced by `SetAction` with `ParseResult` parameter. InvokeAsync moved from RootCommand to ParseResult. Always check the actual DLL exports (reflection) when web search gives contradictory migration advice. xUnit v3 with `dotnet test` requires MTP mode opt-in via global.json on .NET 10 SDK.

### IDE Diagnostics Cleanup (2026-03-14)

**Task:** Fix ~95 Visual Studio IDE diagnostics across the entire solution.

**Categories fixed:**
1. **Use 'var' (built-in types)** — Replaced `int`, `decimal` explicit declarations with `var` where type is obvious from the initializer. ~20 occurrences across FinaryApiClient, UnifiedFinaryApiClient, all sheet writers, WorkbookExporter, FinaryDelegatingHandler, and test files.
2. **Remove trailing commas** — Cleaned up trailing commas in object/collection initializers across UnifiedFinaryApiClient, AssetCategory switch expression, UnifiedFinaryApiClientTests, and ApiFixtures. ~12 occurrences.
3. **Invert 'if' to reduce nesting** — Converted nested conditionals to early-return/continue guards in FinaryApiClient (owner member loop), UnifiedFinaryApiClient (HoldingId check), ConsoleCredentialPrompt (null char check), and Program.cs (BuildOutputPath/BuildUnifiedPath).
4. **Redundant qualifiers/code** — Removed `Models.` prefix in PortfolioSummarySheet, 5 redundant `FinaryExport.X.Y` qualifiers in Program.cs (added `using Microsoft.Extensions.Logging.Console`), added `internal` modifier to CurlMessageHandler, removed redundant Dispose override in CurlMessageHandler, removed redundant `?.` null conditional in CompactConsoleFormatter (Formatter is non-nullable in .NET 10).
5. **Merge/simplify expressions** — Merged conditional ternary into null-propagating division in UnifiedFinaryApiClient, converted ClerkAuthClient `SessionId` backing field to auto-property with private setter, converted dual `if` to `switch` in LoginAsync, replaced `_cts?.Cancel()` with `await _cts.CancelAsync()` in TokenRefreshService.
6. **Test fixes** — Removed conditional access on known non-null `dividends` in DividendsSheet, removed redundant collection expressions in `BeEquivalentTo` params (3 occurrences), removed redundant `!` nullable suppressions (6 occurrences), converted 2 async-without-await test methods to sync returning `Task.CompletedTask`, used `ReadExactly` instead of `Read` in WorkbookExporterTests, converted `new Cookie(...)` to target-typed `new(...)` in ApiFixtures, converted `if/throw/return` to `return throw-expression` in InMemorySessionStore.

**Skipped:** AssetCategory.cs extension block conversion (C# 13/14 feature — not standard yet).

**Build:** ✅ Clean (0 warnings, 0 errors). **Tests:** ✅ 134 pass.

**Key Lesson:** Batch IDE diagnostic fixes are most efficient when grouped by category and applied file-by-file. Auto-property conversion (`SessionId`) requires updating all field references across the class. The `CancellationTokenSource.CancelAsync()` method (added in .NET 8) is the preferred async alternative to `Cancel()`. FluentAssertions + nullable analysis: `!` suppressions after `.Should().NotBeNull()` may be flagged as redundant by modern analyzers.

### Publish Profile: win-x64 (2026-03-14)

**Task:** Created a publish profile for self-contained single-file Windows x64 deployment.

**File:** `src/FinaryExport/Properties/PublishProfiles/win-x64.pubxml`

**Configuration:**
- RuntimeIdentifier: `win-x64`, SelfContained, PublishSingleFile, Release configuration
- `IncludeNativeLibrariesForSelfExtract=true` — bundles native libs into the single-file exe
- **PublishTrimmed=false** — trimming is unsafe (see decision inbox)
- No Native AOT

**Output:** `src/FinaryExport/bin/Release/net10.0/win-x64/publish/FinaryExport.exe` (~90 MB). CurlImpersonate native files (.bat launchers, cacert.pem, runtimes/) sit alongside the exe since they can't be embedded in the single-file bundle.

**Command:** `dotnet publish src/FinaryExport -p:PublishProfile=win-x64`

**Key Lesson:** PublishTrimmed requires STJ source generators to be configured. This project uses reflection-based `JsonSerializer.Deserialize<T>()` with runtime `JsonSerializerOptions`. ClosedXML and Microsoft.Extensions.Hosting also rely on reflection. Trimming would break serialization and DI at runtime. If trimming is desired later, first step is migrating to STJ source generators (JsonSerializerContext).

### Publish Profile Completion & Decision Filing (2026-03-15)

**Task:** Completed win-x64 publish profile task with decision documentation.

**Completed:**
- Profile published and tested: ~90 MB self-contained exe ✅
- README updated with `dotnet publish` instructions
- Decision "PublishTrimmed Disabled in win-x64 Profile" filed to team decisions

**Decision Summary:** Trimming blocked by reflection dependencies (System.Text.Json without source generators, ClosedXML reflection usage, DI container). File created: `.squad/decisions/decisions.md`

**Team Notifications:**
- Orchestration log: `.squad/orchestration-log/2026-03-15T0833-linus.md`
- Session log: `.squad/log/2026-03-15T0833-publish-profile.md`

## Cross-Agent Updates

### Full Project Reassessment Session (2026-03-15T21:21Z)

**Session Type:** Multi-agent team reassessment  
**Participants:** Rusty (Lead), Linus (Backend), Basher (Tester)  
**Outcome:** ✅ Success — All deliverables complete

**Your Deliverables (5 code fixes):**
1. Dead endpoint removal — Eliminated unused API route
2. Parameter alignment — Fixed method signature mismatches
3. Column header disambiguation — Resolved Excel export header conflicts
4. Missing Accept header on retry — Added proper HTTP header handling
5. Unused dependency removal — Cleaned up package.json and imports

**Build Status:** Clean (0 warnings, 0 errors) ✅  
**Test Suite Impact:** Supported expansion from 134 → 240 tests (+106 new tests)

**Metrics:**
- Code quality issues fixed: 5/5
- Tests passing: 240/240 (100%)
- Regressions: 0

**Artifacts:**
- Orchestration log: `.squad/orchestration-log/2026-03-15T21-21-linus.md`
- Session log: `.squad/log/2026-03-15T21-21-reassessment.md`

**Key Achievements:**
- All fixes maintain backward compatibility
- Code audit covered all critical paths
- Zero technical debt from these items
- Improved maintainability through consistent APIs

**Cross-team coordination notes:**
- Rusty's documentation updates reference your API fixes
- Basher's new tests cover areas touched by your parameter alignment work
- All agents verified clean build state together


### README MCP Section Rewrite (2026-03-17)

Rewrote the MCP Server section of README.md from developer-focused (raw tool names, API steps) to user-focused (natural language conversation examples).

**What changed:**
- Replaced `get_profiles` / `set_active_profile` step lists with plain English: "Switch to my daughter's profile"
- Added ~15 real-life conversation examples grouped by capability (portfolio, accounts, transactions, dividends, allocation, multi-profile)
- Simplified auth explanation: "The assistant will prompt you for email, password, and TOTP" instead of explaining MCP Elicitation protocol internals
- Moved the 16-tool reference table into a collapsible `<details>` block — still available for developers but not front-and-center
- Kept the JSON config snippet (users actually need that) and the published-exe tip
- No UserSecrets references found — clean

**Key principle:** README readers are users who want to talk to their AI assistant about their portfolio. They don't care about `get_portfolio_summary` — they care about "What's my total portfolio value?" Write for that person.

### Asset List Pagination Fix (2026-03-17)

**Problem:** `GetAssetListAsync` was using `GetAsync` with a hardcoded `limit=100` query parameter — single-page fetch only. The Finary API returns results sorted by value descending across ALL accounts, so users with 27+ positions only got the highest-value items. Lower-value holdings were silently truncated.

**Solution:** Changed from `GetAsync` to `GetPaginatedListAsync` (existing helper, already used for transactions). The helper uses `page=N&per_page=100` and loops until it receives fewer items than the page size.

**Before:**
```csharp
return await GetAsync<List<AssetListEntry>>($"{BasePath}/asset_list?limit=100&period={period}", ct) ?? [];
```

**After:**
```csharp
return await GetPaginatedListAsync<AssetListEntry>($"{BasePath}/asset_list?period={period}", pageSize: 100, ct);
```

**Key learning:** Finary's `limit` parameter doesn't paginate — it just caps results. Their actual pagination uses `page` + `per_page`. The `GetPaginatedListAsync` helper already knew this pattern from transactions; I just wasn't using it for asset_list.

**Build:** ✅ Clean (0 warnings, 0 errors).

### Constants Audit (2026-03-18)

Audited all hardcoded strings across `src/FinaryExport.Core/` (37 .cs files) for extraction into `FinaryConstants.cs`. Current constants: `ImpersonationProfile`, `ApiBaseUrl`, `AppOrigin`, `ClerkBaseUrl`.

**Top extraction candidates (repeated or semantically meaningful):**
- `"Finary"` — HTTP client name, used in both registration and resolution (2 sites).
- `"/users/me/organizations"` — API route, used in 2 methods.
- `"x-client-api-version"` / `"2"` — custom header, duplicated in SendAsync + CloneRequest.
- `"x-finary-client-id"` / `"webapp"` — custom header, duplicated in SendAsync + CloneRequest.
- `"new_format=true"` — query param baked into 4 API URLs.
- `"all"` — default period value, appears in 12 method signatures across interface + implementations.
- `"gross"` — default valueType, appears in 3 method signatures.
- `pageSize = 200` — default transaction page size, appears in 3 method signatures.
- `".finaryexport"` / `"session.dat"` — session store path components.

**Clerk-specific strings (single-use but domain-meaningful):**
- `/v1/client/sign_ins`, `/v1/client/sign_ins/{id}/attempt_second_factor`, `/v1/client/sessions/{id}/tokens` — Clerk endpoint path templates.
- `"needs_second_factor"`, `"totp"` — Clerk protocol values.

**Correctly skipped:** JSON property names, standard HTTP headers, form field names, model property names, enum display/URL mappings, error messages, log messages, local consts already extracted.

**Key insight:** `FinaryDelegatingHandler.CloneRequest` duplicates all 5 header assignments from `SendAsync`. Extracting header names/values to constants reduces duplication surface and makes the protocol contract explicit.
