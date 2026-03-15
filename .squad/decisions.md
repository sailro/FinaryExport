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

Architecture document: `architecture.md`

**Key Decisions:**
1. **Single project** — namespace separation, not multi-project. This is a CLI tool.
2. **ClosedXML** for xlsx — MIT, active. EPPlus rejected (commercial).
3. **No Polly** — retry logic hand-rolled. Too simple to justify a dependency.
4. **`ITokenProvider`** — sole auth abstraction. Nothing outside Auth/ knows about Clerk.
5. **One `FinaryApiClient`** — partial classes by concern, category endpoints parameterized by enum.
6. **`record` types, `decimal` for money, STJ source generators, `SnakeCaseLower`.**
7. **Per-category error isolation** — one failing category cannot kill the export.
8. **`PeriodicTimer` token refresh** as `IHostedService` (50s interval).
9. **Credentials via env vars / user secrets only.** Never in appsettings.json.
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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
