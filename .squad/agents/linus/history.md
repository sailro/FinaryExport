# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Learnings

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
