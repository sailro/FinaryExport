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
- **2026-03-12 — Security audit (D-pii).** All real PII scrubbed from source and tests. Synthetic names used: Jean Dupont, Marie Dupont, Claire Dupont. Test fixtures use synthetic IBANs, account names, institution names.
- **2026-03-12 — Code style (D-noxml).** No XML doc comments (`///`). Regular comments (`//`) only, used sparingly for non-obvious logic.
- **Current tech stack:** .NET 10, C# 14, ClosedXML 0.104.2, Loxifi.CurlImpersonate 1.1.0, Microsoft.Extensions.Hosting 9.0.4, System.CommandLine 2.0.0-beta4, System.Security.Cryptography.ProtectedData 9.0.4.
- **Current sheets (5):** Summary, Accounts (one per category), Transactions, Dividends, Holdings.
- **Test suite:** 134 tests passing (xUnit/NUnit), 13 test files covering Auth, API, Export, Infrastructure.
- **2026-03-14 — Cross-platform scoping (rusty-cross-platform).** Analyzed two Windows-only blockers: DPAPI session encryption and CurlImpersonate TLS bypass. Key findings: (1) DPAPI → `Microsoft.AspNetCore.DataProtection` is a clean swap behind existing `ISessionStore` interface — Small effort. (2) `Loxifi.CurlImpersonate` 1.1.0 ships `linux-x64` native binary (`libcurl-impersonate.so`) despite README claiming Windows-only — needs verification but likely works. macOS has zero native binaries and no build path — Hard blocker. Recommended minimum viable path: Windows + Linux x64, ~1 day total work. macOS deferred. Scoping doc at `.squad/decisions/inbox/rusty-cross-platform.md`.
- **2026-03-14 — Documentation audit.** Audited README.md, architecture.md, api-analysis.md against source code. Key drift found and fixed: (1) CLI options had stale `--locale` (removed) and wrong period values (`ytd` → `1d/3m/6m`). (2) Package versions were outdated — ClosedXML 0.104.2→0.105.0, Microsoft.Extensions.* 9.0.4→10.0.5, System.CommandLine 2.0.0-beta4→2.0.5. (3) `dotnet run` commands missing `--project src/FinaryExport`. (4) FinaryOptions showed removed `Locale` property. (5) Architecture missing Directory.Build.props/Directory.Packages.props, xUnit v3, DividendAssetInfo, HasTransactions() filtering, SetAction API. (6) Models/Auth folder was listed with content but is empty. (7) Short option aliases `-o`/`-p` documented but don't exist in code.
