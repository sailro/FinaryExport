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

