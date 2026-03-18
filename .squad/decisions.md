# Squad Decisions

## Active Decisions

### 2026-03-14: Cross-Platform Support Scoping (Pending Decision)

**Author:** Rusty (Lead)  
**Scope:** Infrastructure / Platform  
**Status:** Scoping / Awaiting approval

Analyzed feasibility of cross-platform support (Windows, Linux, macOS) against two blockers: DPAPI encryption and CurlImpersonate TLS bypass.

**Key Findings:**
1. **DPAPI (Session Encryption)** — Windows-only via `System.Security.Cryptography.ProtectedData`. Solution: swap to `Microsoft.AspNetCore.DataProtection` (cross-platform, encryption at rest). Effort: **Small** (~0.5 day).
2. **CurlImpersonate (TLS Bypass)** — NuGet package ships Linux x64 binary (untested); macOS has no binaries and no build path. Effort for Linux: **Small** (~0.5 day verification). macOS: **Large/blocked** due to build complexity.

**Recommendation:**
- **Windows + Linux x64:** Proceed. ~1 day total work. Clear architecture, existing `ISessionStore` abstraction supports swap.
- **macOS:** Defer. Loxifi upstream gaps + build system complexity. Revisit if demand materializes or Loxifi adds macOS support.

**Decision Needed:** Approve Linux support work? (Would update D13 to remove "Windows-only by design" constraint.)

**Details:** See `.squad/decisions/inbox/rusty-cross-platform.md` (full analysis with alternatives, risks, implementation sketches).

---

### 2026-03-14: Transaction-Capable Categories via HasTransactions()

**Author:** Linus (Backend)  
**Scope:** Models / Export

The Finary API only supports `/portfolio/{category}/transactions` for 4 of 10 `AssetCategory` values: Checkings, Savings, Investments, Credits. The other 6 return HTTP 404. Querying them produced 30 misleading warnings per export run.

**Decision:** Added `AssetCategory.HasTransactions()` extension method in `AssetCategoryExtensions`. Callers that iterate categories for transaction queries should filter with `.Where(c => c.HasTransactions())`.

The per-category try/catch in `TransactionsSheet` is retained as a safety net for genuine errors on supported categories.

**Impact:**
- If Finary adds transaction support for new categories in the future, update the switch expression in `HasTransactions()`.
- Any new code iterating categories for transactions should use this filter.

---

### 2026-03-12: FinaryExport Architecture Blueprint

**Author:** Rusty (Lead)  
**Scope:** Entire project  
**Status:** ⚠️ Partially superseded — "single project" is now multi-project (Core + CLI + MCP per D-mcp-complete)

Architecture document: `architecture.md`

**Key Decisions:**
1. ~~**Single project** — namespace separation, not multi-project.~~ **Superseded:** Solution now has 3 projects (FinaryExport.Core, FinaryExport, FinaryExport.Mcp) per D-mcp-complete. Core uses `RootNamespace=FinaryExport` to preserve zero-namespace-change extraction.
2. **ClosedXML** for xlsx — MIT, active. EPPlus rejected (commercial).
3. **No Polly** — retry logic hand-rolled. Too simple to justify a dependency.
4. **`ITokenProvider`** — sole auth abstraction. Nothing outside Auth/ knows about Clerk.
5. **One `FinaryApiClient`** — partial classes by concern, category endpoints parameterized by enum.
6. **`record` types, `decimal` for money, STJ source generators, `SnakeCaseLower`.**
7. **Per-category error isolation** — one failing category cannot kill the export.
8. **`PeriodicTimer` token refresh** as `IHostedService` (50s interval).
9. ~~**Credentials via env vars / user secrets only.**~~ **Superseded:** Credentials are entered interactively via `ConsoleCredentialPrompt` (CLI) or `McpCredentialPrompt` (MCP Elicitation). No env vars, no user secrets.
10. **Generic host** (`Host.CreateApplicationBuilder`) for DI, config, logging.

**Impact:** All team members implementing code follow this architecture. Linus builds from here.

---

### 2026-03-12: User Directive — Premium Models for Agent Spawns

**By:** the user (via Copilot)

Always use premium models for agent spawns — no cost-tier restrictions.

---

### 2026-03-12: Session Persistence via ISessionStore (D13)

**Author:** Rusty (Lead)  
**Scope:** Auth Module

Add cookie/session persistence to skip the full 6-step Clerk auth on subsequent runs.

**Key Points:**
1. **`ISessionStore` interface** — `SaveSessionAsync`, `LoadSessionAsync`, `ClearSessionAsync`. Abstracts storage; default implementation uses DPAPI-encrypted file.
2. **Two-tier auth flow** — Warm start (load cookie → `/tokens` → JWT) tried first. Cold start (full 6-step) as fallback on 401 or missing session.
3. **Encrypted at rest** — DPAPI (`DataProtectionScope.CurrentUser`) on Windows. `IDataProtectionProvider` as cross-platform fallback.
4. **Non-fatal** — Session store failures never block auth. Missing/corrupted store → cold start. Failed save → warning only.
5. **Configurable** — `SessionStorePath` in `FinaryOptions` (default: `~/.finaryexport/session.dat`). `clear-session` is a standalone CLI command that calls `ISessionStore.ClearSessionAsync()`.
6. **Architecture doc updated** — `architecture.md` revised with Auth Module section, project structure, configuration, execution flow, error handling.

**Rationale:** The `__client` cookie has ~90-day expiry and survives token refreshes. Persisting it eliminates 5 of 6 auth requests on every run after the first.

**Impact:** Linus implements `ISessionStore.cs`, `EncryptedFileSessionStore.cs`. `ClerkAuthClient` gains warm start logic. Auth API (`ITokenProvider`) unchanged.

---

### 2026-03-12: CurlImpersonate for TLS Fingerprint Bypass (D-curl)

**Author:** Rusty (Lead) / Livingston (Protocol)  
**Scope:** Auth + Infrastructure

Adopt `Loxifi.CurlImpersonate` 1.1.0 with `BrowserProfile.Chrome136` for Chrome TLS fingerprint impersonation.

**Key Points:**
1. Cloudflare detects TLS fingerprints — .NET's `SslStream` is flagged regardless of HTTP headers.
2. `CurlClient` used directly in ClerkAuthClient for Clerk API calls (not via HttpClientFactory).
3. `CurlMessageHandler` bridges CurlClient into HttpMessageHandler for Finary API calls, preserving DelegatingHandler pipeline.
4. Pinned to 1.1.0 — native dependency, treat upgrades carefully.

**Alternatives rejected:** Python subprocess (cross-process complexity), TlsClient.NET (unmaintained, .NET 6 only).

**Impact:** Auth and Finary API calls both bypass Cloudflare. ClerkDelegatingHandler removed.

---

### 2026-03-12: Multi-Profile Export (D-multiprofile)

**Author:** Rusty (Lead)  
**Scope:** Export + API

Export one xlsx per Finary membership plus a unified aggregated file.

**Key Points:**
1. `GetAllProfilesAsync()` discovers personal + organization memberships.
2. Per-profile exports use `ExportContext { UseDisplayValues = true }` — ownership-adjusted values from `display_balance`/`display_buying_value`.
3. Unified export uses `UnifiedFinaryApiClient` with `UseDisplayValues = false` — raw computed totals.
4. Output naming: `finary-export-{profile-name}.xlsx` + `finary-export-unified.xlsx`.

**Impact:** Program.cs orchestrates multi-profile loop. ExportContext added. UnifiedFinaryApiClient added.

---

### 2026-03-12: UnifiedFinaryApiClient Decorator (D-unified)

**Author:** Rusty (Lead)  
**Scope:** API

Transparent decorator over `IFinaryApiClient` that aggregates across all memberships.

**Key Points:**
1. Iterates all memberships, deduplicates entities by ID.
2. Shared assets (ownership < 100%) scaled up: `display_balance / share = full_value`.
3. Non-aggregatable endpoints (timeseries, allocations) use owner's data only.
4. Caches merged account list to avoid redundant API calls.

**Rationale:** Sheet writers remain unaware of multi-profile — they work against `IFinaryApiClient` regardless of whether it's per-profile or unified.

---

### 2026-03-12: PII Scrub Policy (D-pii)

**Author:** Team consensus  
**Scope:** Entire codebase + squad files

Synthetic data used in tests and squad files. Real data never stored in source or tracked files.

**Key Points:**
1. Test fixtures use synthetic names and identifiers only.
2. `.squad/` files refer to "the user" — never real names.
3. Export `.xlsx` files (containing real data) are gitignored.

---

### 2026-03-12: Rate Limiter at 5 req/s (D-ratelimit)

**Author:** Livingston (Protocol) / Rusty (Lead)  
**Scope:** Infrastructure

Rate limiter set to 200ms interval (5 requests/second).

**Key Points:**
1. API analysis showed browser making ~2.5 req/s with no rate limit headers from Finary API.
2. 5 req/s provides 2× headroom over observed browser rate.
3. Implemented via `RateLimiter` class with semaphore-based gating.
4. Applied in `FinaryDelegatingHandler` — transparent to API client.

---

### 2026-03-12: CompactConsoleFormatter (D-logging)

**Author:** Linus (Backend)  
**Scope:** Infrastructure

Custom `ConsoleFormatter` for single-line log output: `"dbug: ClerkAuthClient: Token refreshed"`.

**Key Points:**
1. Strips namespace prefixes from category names.
2. Maps log levels to 4-character codes (dbug, info, warn, fail, crit).
3. `appsettings.json`: default level Information, HttpClient suppressed to Warning.

---

### 2026-03-12: No XML Doc Comments (D-noxml)

**Author:** Team consensus  
**Scope:** Entire codebase

No XML doc comments (`///`). Use regular comments (`//`) only, sparingly, for non-obvious logic.

**Rationale:** This is a private CLI tool, not a library. XML docs add noise without value. Code should be self-documenting.

---

### 2026-03-16T08:45:00Z: MCP Architecture & Implementation (D-mcp-complete)

**Authors:** Rusty (architecture), Livingston (API catalog), Linus (Core extraction), Saul (MCP server)  
**Scope:** Solution structure, MCP server implementation, Core library extraction

**Status:** ✅ **COMPLETE** — All four agents delivered on schedule. Build: 0 errors, 0 warnings. Tests: 240/240 passing.

**Key Decisions:**

1. **Extract `FinaryExport.Core` shared library** — ClassLibrary with RootNamespace=FinaryExport (zero namespace changes). Contains all models, API client, auth, infrastructure.

2. **Dual-project solution:** `FinaryExport.Core` (shared) + `FinaryExport` (CLI) + `FinaryExport.Mcp` (new MCP server).

3. **15 MCP tools across 7 tool classes** — UserTools, PortfolioTools, AccountTools, TransactionTools, DividendTools, HoldingsTools, AllocationTools. All read-only (GET).

4. **Stdio transport with ModelContextProtocol 1.1.0** — standard for VS Code Copilot, Claude Desktop.

5. **Elicitation auth (supersedes session-only directive)** — MCP tries warm start from `~/.finaryexport/session.dat` first. If no session exists, `McpCredentialPrompt` uses MCP Elicitation to prompt the user for credentials through the MCP client. Falls back to suggesting a CLI run if the client doesn't support elicitation. No env var credentials, no Otp.NET.

6. **Bootstrap required** — `finary_get_org_context` must be called before any data tool to set org/membership context.

7. **Per-category error isolation** — multi-category tools aggregate with isolated error handling per category.

**Details:**

- **Core extraction:** 240 tests passing. All dependencies preserved. Zero refactoring needed in consumer code.
- **MCP server:** `Program.cs` uses `Host.CreateApplicationBuilder` + `AddFinaryCore()` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`.
- **Tool catalog:** 15 tools → 37 model types. Pagination internal (transparent to MCP). Rate limiting at infrastructure layer.
- **Auth:** `McpCredentialPrompt` throws if `session.dat` doesn't exist (user must run CLI first). `TokenRefreshService` keeps JWT alive during server lifetime.

**Artifacts:**
- `.squad/decisions/inbox/rusty-mcp-architecture.md` (full proposal, 25.7 KB)
- `.squad/artifacts/mcp-tool-catalog.md` (tool + model inventory, 3.2 KB)
- `.squad/orchestration-log/2026-03-16T0845-{rusty,livingston,linus,saul}.md` (per-agent logs)
- `.squad/log/2026-03-16T0845-mcp-server.md` (session summary)

**Implementation Notes:**
- Service registration split: `AddFinaryCore()` in Core (auth, API, config) — zero mention of export/CLI/MCP dependencies. Each consumer registers own `ICredentialPrompt` + `ISessionStore`.
- Tool names are snake_case. Descriptions are LLM-facing (concise, specific, no jargon).
- Console logging redirected to stderr via `ConsoleLoggerOptions.LogToStandardErrorThreshold = LogLevel.Trace` (stdout reserved for MCP protocol).

**Open Questions (resolved by user directive):**
- ✅ Session reuse — YES, shared `session.dat` across CLI and MCP per copilot-directive-20260316T084500Z
- ✅ Otp.NET — NO, removed from proposal. Session-only auth removes TOTP/env-var credential need.

---

### 2026-03-16T08:45:00Z: User directive — MCP auth via shared session

**By:** the user (via Copilot)  
**Status:** ⚠️ Partially superseded — MCP now supports cold auth via Elicitation (no longer requires session.dat)  
**Original:** The MCP server must reuse the session.dat created by the CLI exporter. No cold auth / no env var credentials / no TOTP in the MCP server. If no session.dat exists, explain that a first export run is needed to initialize it.  
**Current:** MCP server still tries session.dat first (warm start), but `McpCredentialPrompt` uses MCP Elicitation for cold auth if no session exists. The "no env var credentials / no Otp.NET" constraint remains in effect.  
**Why:** Elicitation support in MCP SDK made standalone cold auth possible without env var complexity.

---

### 2026-03-17T18:45:00Z: Asset List Pagination Fix

**Author:** Linus (Backend)  
**Date:** 2026-03-17  
**Scope:** API Client  
**Status:** ✅ Implemented

## Context

`GetAssetListAsync` in `FinaryApiClient.Portfolio.cs` was fetching assets with `limit=100` — a single-page request. The Finary API returns results sorted by total value descending across ALL accounts. Users with more than ~27 positions were silently losing their lower-value holdings from exports.

## Decision

Changed `GetAssetListAsync` to use the existing `GetPaginatedListAsync<T>` helper (already used for transactions). This switches from `limit=100` (single fetch) to `page=N&per_page=100` (pagination loop).

## Rationale

- The pagination helper already exists and is battle-tested (transactions use it).
- Finary's `limit` parameter caps results but doesn't enable pagination.
- Finary's actual pagination uses `page` + `per_page` query parameters.
- The helper loops until `batch.Count < pageSize`, ensuring all results are fetched.

## Impact

- **Fixed:** Users with 27+ total positions now get complete asset lists.
- **No breaking changes:** Method signature unchanged, behavior improved.
- **Future guidance:** Any new list endpoints should use `GetPaginatedListAsync`, not `GetAsync` with a `limit` parameter.

## Files Changed

- `src/FinaryExport.Core/Api/FinaryApiClient.Portfolio.cs`

---

### 2026-03-17T18:45:00Z: Full Pagination Audit — Zero Issues

**Author:** Livingston (Protocol Analyst)  
**Date:** 2026-03-17  
**Scope:** All HTTP API call sites in FinaryExport.Core and FinaryExport.Mcp
**Status:** ✅ Complete

## Audit Results

**No pagination bugs found.** The `GetAssetListAsync` issue identified by the team has already been fixed by Linus — it now uses `GetPaginatedListAsync`.

## Scope

- 14 API methods in `FinaryApiClient.cs` (all partials)
- 7 MCP tool classes (all wrappers around `IFinaryApiClient`)
- 2 decorator clients (`UnifiedFinaryApiClient`, `AutoInitFinaryApiClient`)

## Key Findings

| Status | Count | Details |
|--------|-------|---------|
| ✅ Properly paginated | 2 | `GetCategoryTransactionsAsync`, `GetAssetListAsync` |
| ➖ Safe (bounded lists/single objects) | 12 | Accounts, timeseries, organizations, allocations, etc. |
| ❌ Needs pagination | 0 | **None** |

## Endpoints Verified Safe (No Pagination Needed)

- `GetCategoryAccountsAsync` — Bounded by real-world account count (1-10 typically)
- `GetHoldingsAccountsAsync` — Bounded by brokerage account count
- `GetPortfolioTimeseriesAsync` / `GetCategoryTimeseriesAsync` — Bounded by time period density
- `GetOrganizationContextAsync` / `GetAllProfilesAsync` — Bounded by memberships (1-3 typically)

## MCP Tool Layer

All MCP tools are pure wrappers around `IFinaryApiClient`. They make no direct HTTP calls and add no pagination limits. Pagination correctness is inherited from the underlying API client.

**Verified:** UserTools, PortfolioTools, AccountTools, TransactionTools, DividendTools, HoldingsTools, AllocationTools

## Future Guidance

1. Any new endpoint returning `List<T>` for **transactions**, **holdings**, or **positions** should use `GetPaginatedListAsync`
2. **Accounts** and **timeseries** endpoints are safe unpaginated
3. Always check wire traffic for `page`/`per_page` support when adding new endpoints

---

### 2026-03-17T18:45:00Z: User directive — No PII in squad reports

**By:** the user (via Copilot)  
**What:** Never record PII, account numbers, real financial data, or other personally identifiable information when writing squad reports, decision files, audit documents, or any `.squad/` artifacts.  
**Why:** User request — reinforces existing D-pii policy. Captured for team memory so all agents sanitize output.

---

### 2026-03-18T08:22:00Z: Constants Extraction — ApiPaths Naming

**Author:** Linus (Backend)  
**Date:** 2026-03-18  
**Scope:** FinaryConstants / naming conventions

## Context

Extracted 11 magic strings, header patterns, and default values from across FinaryExport.Core and FinaryExport into centralized `FinaryConstants` nested classes.

## Decision

Renamed the initial `Api` nested class to `ApiPaths` to avoid namespace collision with `FinaryExport.Api`. The C# compiler resolves `Api.X` to the namespace rather than the `using static`-imported nested class, causing CS0234 errors in any file that imports or resides in `FinaryExport.Api`.

## Impact

- All references use `ApiPaths.HttpClientName`, `ApiPaths.UsersOrganizationsPath`, `ApiPaths.CurrentUserPath`
- `Headers` and `Defaults` nested classes had no conflicts and kept their original names
- Future nested classes in `FinaryConstants` should avoid names that match existing namespace segments (e.g., `Api`, `Models`, `Infrastructure`)
- `SetFinaryHeaders` helper extracted in `FinaryDelegatingHandler` to DRY up header-setting between `SendAsync` and `CloneRequest`
- All default parameter values (`"all"`, `"gross"`, `200`) across the interface chain now reference `Defaults.*` constants

## Build Verification

- Build: clean (0 errors, 0 warnings)
- Tests: 240/240 passing

---

### 2026-03-18T08:51:00Z: Crypto Holdings Export Implementation (D-crypto)

**Authors:** Linus (Backend), Basher (Tester)  
**Scope:** Models / Export  
**Status:** ✅ COMPLETE — Build 0 errors/warnings, 263 tests passing

## Context

The Finary API returns crypto position data nested inside each account from `GetCategoryAccountsAsync("cryptos")`. The `Account.Cryptos` field was typed as `JsonElement?` and never deserialized, meaning crypto holdings were silently ignored in exports.

## Decision

1. Created typed models `CryptoPosition` and `CryptoInfo` (plus `FiatPosition`/`FiatInfo` for fiat data) following the `SecurityPosition`/`SecurityInfo` pattern.
2. Changed `Account.Cryptos` from `JsonElement?` to `List<CryptoPosition>?` and `Account.Fiats` from `JsonElement?` to `List<FiatPosition>?`.
3. Created `CryptoHoldingsSheet` following the `HoldingsSheet` pattern — flattens crypto positions into spreadsheet rows.
4. Registered `CryptoHoldingsSheet` in `Program.cs` DI as an `ISheetWriter`.

## Rationale

- Existing `HoldingsSheet` only covers `AssetCategory.Investments` securities. Crypto is separate with different nested data shape.
- Typed deserialization enables compile-time safety and LINQ queries vs manual `JsonElement` traversal.
- `FiatPosition` typed simultaneously since data shape nearly identical and both were `JsonElement?`.

## Impact

- Exports now include "Crypto Holdings" sheet: Account, Name, Code, Quantity, Buy Price, Current Price, Value, Buy Value, +/- Value, +/- %.
- Multi-profile works transparently — positions nested in accounts, already deduplicated by `UnifiedFinaryApiClient`.
- No API changes needed — data was already being fetched, just not deserialized.

## Files Changed

**Created:**
- `src/FinaryExport.Core/Models/Accounts/CryptoPosition.cs`
- `src/FinaryExport.Core/Models/Accounts/CryptoInfo.cs`
- `src/FinaryExport.Core/Models/Accounts/FiatPosition.cs`
- `src/FinaryExport/Export/Sheets/CryptoHoldingsSheet.cs`
- `tests/FinaryExport.Core.Tests/Models/CryptoPositionTests.cs`
- `tests/FinaryExport.Core.Tests/Models/CryptoInfoTests.cs`
- `tests/FinaryExport.Core.Tests/Models/FiatPositionTests.cs`
- `tests/FinaryExport.Tests/Export/Sheets/CryptoHoldingsSheetTests.cs`

**Modified:**
- `src/FinaryExport.Core/Models/Accounts/Account.cs`
- `src/FinaryExport/Program.cs`

## Test Coverage

23 new tests (263 total):
- CryptoPosition deserialization: null handling, dust amounts (1 satoshi), zero quantity, negative P&L, extreme prices
- CryptoInfo nested metadata: missing symbol fallback, null image URL, unicode symbols
- FiatPosition: null FiatInfo, currency codes, P&L scaling for shared assets
- CryptoHoldingsSheet: column structure, multiple positions per account, null filtering, account ordering, P&L styling, empty accounts, full export integration

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
