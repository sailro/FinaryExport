# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-03-12 — Architecture defined.** Wrote `architecture.md` as the implementation blueprint. Key decisions:
  - Single project, namespace separation (not multi-project). CLI tool doesn't warrant assembly overhead.
  - ClosedXML for xlsx (MIT, active). EPPlus rejected (commercial license).
  - No Polly — retry logic is trivial for a run-once tool. Hand-rolled in delegating handler.
  - `ITokenProvider` is the only auth surface visible to the rest of the app. Decouples from Clerk internals.
  - One `FinaryApiClient` split into partial classes by concern. Category endpoints are parameterized by enum, not separate clients.
  - `record` types, `decimal` for money, STJ source generators with `SnakeCaseLower`.
  - Per-category error isolation: one failing category doesn't kill the export.
  - `PeriodicTimer`-based token refresh as `IHostedService` (50s interval, JWT TTL is 60s).
  - Credentials via env vars / user secrets. Never in `appsettings.json`.
  - Generic host (`Host.CreateApplicationBuilder`) for DI, config, logging, hosted services.
- **Key file paths:** `architecture.md` (blueprint), `api-analysis.md` (protocol reference)
- **Owner preference:** the user wants opinionated decisions, not options lists. Direct and decisive.
- **2026-03-12 — Session persistence (D13).** Added `ISessionStore` + `EncryptedFileSessionStore` to architecture. Two-tier auth: warm start (load persisted `__client` cookie → `/tokens` → JWT) tried first, cold start (full 6-step) as fallback. DPAPI encryption at rest. Non-fatal — store failures never block auth. New config: `SessionStorePath`, `--clear-session`. `ITokenProvider` API unchanged — no impact outside Auth/.
- **2026-03-12 — Auth rewrite: CurlImpersonate (D-curl).** After two failed approaches (Python subprocess, TlsClient.NET), adopted `Loxifi.CurlImpersonate` (pinned 1.1.0) for Chrome TLS fingerprint impersonation. Bypasses Cloudflare bot detection entirely. ClerkAuthClient now uses `CurlClient(BrowserProfile.Chrome136)` directly for Clerk calls. Finary API calls use `CurlMessageHandler` adapter bridging CurlClient into HttpClientFactory. Removed `ClerkDelegatingHandler`, simplified auth to 3-step cold start (sign_in → 2FA → extract session). Interactive credential prompts via `ICredentialPrompt` + `ConsoleCredentialPrompt`.
- **2026-03-12 — Multi-profile export (D-multiprofile).** Added `GetAllProfilesAsync` to enumerate all memberships across organizations. Export produces one xlsx per profile (ownership-adjusted via `display_balance`/`display_buying_value`) plus a unified aggregated file. Per-profile exports use `ExportContext { UseDisplayValues = true }`, unified uses raw totals.
- **2026-03-12 — UnifiedFinaryApiClient decorator (D-unified).** Implements `IFinaryApiClient` as a transparent decorator. Aggregates accounts, transactions, dividends, holdings across all memberships by merging on entity IDs. Shared assets (ownership < 100%) scaled up via `display_balance / share`. Owner's timeseries/allocations used for non-aggregatable endpoints. Account cache prevents redundant API calls.
- **2026-03-12 — Holdings sheet added.** New `HoldingsSheet` exports individual security positions from investment accounts: ISIN, symbol, quantity, buy price, current price, value, unrealized P&L. Flattens Account→SecurityPosition→SecurityInfo hierarchy.
- **2026-03-12 — Rate limiter tuned (D-ratelimit).** Increased from 2 req/s to 5 req/s (200ms interval). API analysis showed browser makes ~2.5 req/s with no rate limit headers observed from Finary's API.
- **2026-03-12 — CompactConsoleFormatter (D-logging).** Custom `ConsoleFormatter` producing single-line logs: `"dbug: ClerkAuthClient: Token refreshed"`. Strips namespace prefixes, maps log levels to 4-char codes.
- **2026-03-12 — Dead code removal.** Removed `ClerkDelegatingHandler`, `FinaryJsonContext` (STJ source gen), unused Auth models (`ClerkTokenResponse`, `SignInResponse`, `SessionResponse`), `AccountDetail`, Otp.NET dependency. Streamlined to essential code only.
- **2026-03-12 — Data hygiene (D-pii).** Synthetic data used in test fixtures only. Real data never stored in source or tracked files.
- **2026-03-12 — Code style (D-noxml).** No XML doc comments (`///`). Regular comments (`//`) only, used sparingly for non-obvious logic.
- **Current tech stack:** .NET 10, C# 14, ClosedXML 0.104.2, Loxifi.CurlImpersonate 1.1.0, Microsoft.Extensions.Hosting 9.0.4, System.CommandLine 2.0.0-beta4, System.Security.Cryptography.ProtectedData 9.0.4.
- **Current sheets (5):** Summary, Accounts (one per category), Transactions, Dividends, Holdings.
- **Test suite:** 134 tests passing (xUnit/NUnit), 13 test files covering Auth, API, Export, Infrastructure.
- **2026-03-14 — Cross-platform scoping (rusty-cross-platform).** Analyzed two Windows-only blockers: DPAPI session encryption and CurlImpersonate TLS bypass. Key findings: (1) DPAPI → `Microsoft.AspNetCore.DataProtection` is a clean swap behind existing `ISessionStore` interface — Small effort. (2) `Loxifi.CurlImpersonate` 1.1.0 ships `linux-x64` native binary (`libcurl-impersonate.so`) despite README claiming Windows-only — needs verification but likely works. macOS has zero native binaries and no build path — Hard blocker. Recommended minimum viable path: Windows + Linux x64, ~1 day total work. macOS deferred. Scoping doc at `.squad/decisions/inbox/rusty-cross-platform.md`.
- **2026-03-14 — Documentation audit.** Audited README.md, architecture.md, api-analysis.md against source code. Key drift found and fixed: (1) CLI options had stale `--locale` (removed) and wrong period values (`ytd` → `1d/3m/6m`). (2) Package versions were outdated — ClosedXML 0.104.2→0.105.0, Microsoft.Extensions.* 9.0.4→10.0.5, System.CommandLine 2.0.0-beta4→2.0.5. (3) `dotnet run` commands missing `--project src/FinaryExport`. (4) FinaryOptions showed removed `Locale` property. (5) Architecture missing Directory.Build.props/Directory.Packages.props, xUnit v3, DividendAssetInfo, HasTransactions() filtering, SetAction API. (6) Models/Auth folder was listed with content but is empty. (7) Short option aliases `-o`/`-p` documented but don't exist in code.
- **2026-03-17 — Documentation audit #2.** Full reassessment of all docs against every source file. Key drift found and fixed: (1) `--period` and `--clear-session` options listed on export command but don't exist — `--output` is the only option; `clear-session` is a standalone command. (2) FinaryOptions showed `Period` and `ClearSession` properties that were removed. (3) ExportContext showed `Period` property — replaced with actual `DisplayCurrencySymbol` and `CurrencyFormat`. (4) Architecture said `ApiEnvelope<T>` but code uses `FinaryResponse<T>` + `FinaryError`. (5) Missing `FinaryApiClient.TransactionCategories.cs` from project structure and partial class list. (6) Partial class count was 5, now 6. `GetAssetListAsync` was listed under Reference but lives in Portfolio. (7) Currency handling feature (DetectDisplayCurrencySymbolAsync, UiConfiguration, ExcelStyles.GetCurrencyFormat) completely undocumented. (8) TransactionsSheet has 10 columns (including transaction category) but README listed 9. (9) appsettings.json logging key was `System.Net.Http.HttpClient` in docs but `System.Net.Http` in code. (10) D13 decision said `--clear-session` is a CLI flag; corrected to standalone command. (11) api-analysis.md implementation notes updated with all actually-used endpoints including /users/me, /organizations, /asset_list, /transaction_categories, and allocation/fees endpoints. (12) Data model table updated with TransactionCategory, UiConfiguration, DisplayCurrencyInfo, FinaryError. (13) Design decisions table was missing D13 and D-logging.
- **2026-03-17 — MCP architecture proposal (D-mcp).** Designed architecture for adding MCP server to the solution. Key decisions: (1) Extract `FinaryExport.Core` shared library — keeps `RootNamespace=FinaryExport` so zero using-statement changes. (2) MCP project (`FinaryExport.Mcp`) uses `ModelContextProtocol` 1.1.0 with stdio transport. (3) 15 read-only MCP tools across 7 tool classes: portfolio, accounts, transactions, dividends, holdings, allocations, user. (4) `EnvCredentialPrompt` for non-interactive auth (env vars + Otp.NET for TOTP generation). (5) Lazy auth on first tool call — same `GetTokenAsync()` fallback pattern. (6) `ServiceCollectionExtensions` split: `AddFinaryCore()` in Core (no export/CLI deps), CLI and MCP each register their own `ICredentialPrompt` + `ISessionStore`. (7) Shared session store at `~/.finaryexport/session.dat` — single auth across CLI and MCP. Full proposal at `.squad/decisions/inbox/rusty-mcp-architecture.md`.
- **Key file paths (MCP):** `.squad/decisions/inbox/rusty-mcp-architecture.md` (full proposal with project files, tool catalog, implementation order)

## Cross-Agent Updates

### MCP Server Implementation Session (2026-03-16T08:45Z)

**Session Type:** Multi-agent parallel sprint — architecture, extraction, and implementation  
**Participants:** Rusty (Lead, architecture), Livingston (Protocol Analyst, API catalog), Linus (Backend Dev, Core extraction), Saul (MCP Specialist, server implementation)  
**Outcome:** ✅ Success — All 4 deliverables on schedule. Build: 0 errors, 0 warnings. Tests: 240/240 passing.

**Key Achievements by Agent:**

1. **Rusty (Architecture):** Designed complete MCP server architecture. Key decisions: dual-project structure (Core library + MCP server), stdio transport, session-only auth per user directive (no env var creds, no Otp.NET), 15 read-only tools, bootstrap requirement, per-category error isolation. Proposal documented with full implementation order and risk mitigation.

2. **Livingston (API Catalog):** Cataloged all 15 `IFinaryApiClient` methods as MCP tools, mapped 37 model types, documented constraints (bootstrap, category transaction filtering, internal pagination), flagged 8 high-value future endpoints, confirmed zero mutations in current surface.

3. **Linus (Core Extraction):** Successfully extracted `FinaryExport.Core` class library. RootNamespace=FinaryExport — zero namespace changes in consumer code. All models, API client, auth, infrastructure moved. Core dependencies (CurlImpersonate, Hosting, Http, DPAPI) preserved. CLI + MCP both reference Core. Build: clean. Tests: 240/240 passing.

4. **Saul (MCP Implementation):** Built `FinaryExport.Mcp` with 15 tools across 7 classes (UserTools, PortfolioTools, AccountTools, TransactionTools, DividendTools, HoldingsTools, AllocationTools). Session-only auth via `McpCredentialPrompt` (throws if no session.dat). Stdio transport via `ModelContextProtocol` 1.1.0. Console logging redirected to stderr. Build: clean. Tests: 240/240 passing.

**User Directive Integration:**
- MCP reuses `~/.finaryexport/session.dat` from CLI
- No cold auth in MCP server
- No env var credentials in MCP (per directive)
- No Otp.NET dependency (removed from proposal after directive)
- Single session source simplifies auth lifecycle

**Orchestration:**
- All agents executed in parallel
- No blockers — Core extraction completed before MCP implementation began
- Clear interface boundaries: Core exposes `AddFinaryCore()`, consumers register own credential/session handlers
- Tool discovery via reflection (`WithToolsFromAssembly()`)

**Impact on Documentation:**
- Merged 4 inbox decision files into decisions.md (deduplicated)
- Updated agents' histories with cross-team context
- Session log written: `.squad/log/2026-03-16T0845-mcp-server.md`
- Per-agent orchestration logs written: `.squad/orchestration-log/2026-03-16T0845-{rusty,livingston,linus,saul}.md`

**Team Confidence:** High — clean architecture, zero test regressions, user directive fully integrated, ready for production deployment.

### Full Project Reassessment Session (2026-03-15T21:21Z)

**Session Type:** Multi-agent team reassessment  
**Participants:** Rusty (Lead), Linus (Backend), Basher (Tester)  
**Outcome:** ✅ Success — All deliverables complete

**Impact on Documentation:**
- 13 drift fixes across architecture.md, README.md, api-analysis.md
- All documentation now synchronized with current implementation
- Build clean (0 warnings, 0 errors)
- Test suite growth: 134 → 240 tests (+106 new tests, +79% growth)

**Key Achievements:**
1. **Architecture:** Fully reassessed and updated. All patterns documented.
2. **Code Quality:** 5 critical fixes (dead endpoints, parameter alignment, headers, dependencies)
3. **Test Coverage:** 106 new tests across 9 new test files covering previously untested components

**Artifacts:**
- Orchestration logs: `.squad/orchestration-log/2026-03-15T21-21-*.md`
- Session log: `.squad/log/2026-03-15T21-21-reassessment.md`
- Decision merger: 3 inbox items merged into decisions.md, deduplicated

**Team Confidence:** High — comprehensive coverage with clean build and 100% test pass rate.

