# Team Decisions

## Decision: Clerk Auth Flow & API Protocol Details

**By:** Livingston (Protocol Analyst)  
**Date:** 2026-03-12  
**Scope:** Auth implementation, API client design  

### Auth Architecture

Finary uses **Clerk** (clerk.finary.com) for authentication. The login is a 6-step flow:
1. GET `/v1/environment` — config
2. GET `/v1/client` — establish client cookies
3. POST `/v1/client/sign_ins` — email + password (form-encoded)
4. POST `/v1/client/sign_ins/{id}/attempt_second_factor` — TOTP code
5. POST `/v1/client/sessions/{id}/touch` — activate session
6. POST `/v1/client/sessions/{id}/tokens` — get JWT

**2FA is mandatory** (TOTP enabled). The tool will need a TOTP secret from the user to generate codes autonomously.

### Token Lifecycle

- JWT is RS256, 60-second TTL
- Refresh via POST to `/tokens` endpoint every ~50 seconds
- Refresh uses `__client` cookie (long-lived), not the JWT itself
- Session lives ~90 days without re-login

### API Protocol

- Base URL: `https://api.finary.com`
- Required headers: `x-client-api-version: 2`, `x-finary-client-id: webapp`
- Response envelope: `{ result, message, error }`
- Endpoints scoped to `/organizations/{org_id}/memberships/{membership_id}/...`
- Pagination: `page` + `per_page` (page-based, 1-indexed)

### Implications for Implementation

- Auth module needs: HttpClient with CookieContainer, TOTP generator, background token refresh timer
- All Clerk calls need `Origin: https://app.finary.com` and query params `__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4`
- Full analysis at `api-analysis.md`

## Decision: Project Structure — src/FinaryExport/ subfolder

**Author:** Linus (Backend Dev)  
**Date:** 2026-03-12  
**Scope:** Project scaffold

The .NET project lives at `src/FinaryExport/` (not repo root), with the solution file `FinaryExport.sln` at the repo root. This keeps the .NET project separate from root-level artifacts (traffic analysis docs, node_modules from httpproxymcp).

### Alternatives Considered

- **csproj at repo root** — Simpler, but mixes .NET build artifacts with traffic capture tooling. The `bin/`, `obj/` folders would clutter alongside `node_modules/` and other root-level artifacts.

### Impact

- Build from repo root: `dotnet build` (picks up the .sln)
- Build from project: `cd src/FinaryExport && dotnet build`
- All source paths in architecture.md map to `src/FinaryExport/{path}`

## Decision: Test Project Uses Contract Stubs (Temporary)

**Author:** Basher (Tester)  
**Date:** 2026-03-12  
**Scope:** Test infrastructure

`FinaryExport.Tests` contains local interface and model stubs in `Contracts/` that mirror `architecture.md`. These let tests compile and pass before the implementation project exists.

### When Linus's Code Lands

Replace the `Contracts/` folder with a `<ProjectReference>` to `FinaryExport.csproj`. Delete the stubs. Tests reference the real interfaces.

### Rationale

Writing tests proactively against stable contracts (from `architecture.md`) catches design issues early. The stubs are minimal copies of the interface signatures — no behavior, just type shapes. The switch to real references will be mechanical.

### Impact

- Linus: no impact on implementation. If interface signatures change, coordinate with Basher to update tests.
- CI: test project builds independently today. Will need solution-level build once `ProjectReference` is added.

## Decision: No XML Doc Comments — Use Regular Comments Only

**By:** the user (via Copilot)  
**Date:** 2026-03-12  
**Scope:** Code style and documentation

All C# code uses regular comments (`//`) instead of XML doc comments (`///`). This is a project-wide style preference.

### Impact

- Implementation code (Linus): Use `//` for all comments, no `///` triple-slash docs
- Test code (Basher): Use `//` for all comments
- IntelliSense: Project will not auto-generate XML doc files, but comments still visible in IDE on hover

## Decision: Interactive Auth (No Stored Credentials)

**Author:** Linus (Backend Dev)  
**Date:** 2026-03-12  
**Scope:** Auth module

### Summary

Auth flow changed from config-based credentials to interactive console prompts. On cold start, the user is prompted for Email, Password, and TOTP Code. Credentials are never persisted — only the `__client` session cookie is stored (via EncryptedFileSessionStore).

### What Changed

- `ICredentialPrompt` interface added — decouples credential acquisition from `ClerkAuthClient`
- `ConsoleCredentialPrompt` — interactive implementation with masked password input
- `TotpSecret`, `Email`, `Password` removed from `FinaryOptions` and config
- `Otp.NET` dependency removed (was only used for TOTP generation from stored secret)

### Impact

- `appsettings.json` only contains `OutputPath`, `Period`, `Locale`
- No credentials in config, user secrets, or env vars
- Tests unaffected (they mock at HTTP layer, don't use FinaryOptions credentials)

## Decision: Auth Flow Simplified + Cloudflare Mitigation

**Author:** Linus (Backend Dev)  
**Date:** 2026-03-12  
**Scope:** Auth Module

### Summary

Rewrote ClerkAuthClient to fix Cloudflare 429 rejections. Three key changes:

1. **Simplified flow:** Cold start is now 3 steps (sign_in → 2FA → extract session), not 6. Skips /v1/environment and /v1/client entirely.
2. **Self-owned HttpClient:** ClerkAuthClient creates its own HttpClient with CookieContainer (no longer uses IHttpClientFactory "Clerk" named client). This gives full cookie jar control for both Cloudflare and Clerk cookies.
3. **Browser fingerprinting:** All Clerk requests now include Chrome-like User-Agent, sec-ch-ua, sec-fetch-*, Accept-Language headers.

### Impact

- **ISessionStore interface changed:** Now saves/loads `SessionData` (SessionId + cookies) instead of raw `IReadOnlyCollection<Cookie>`. Existing session files will trigger a cold start (self-healing).
- **ClerkDelegatingHandler:** No longer registered in DI (its logic moved into ClerkAuthClient). File still exists but is unused.
- **CookieContainer singleton removed from DI** — no longer shared.

### Rationale

Based on Livingston's protocol analysis and FinarySharp's working reference implementation. The 429 was Cloudflare bot detection, not Clerk rate limiting.

## Decision: PublishTrimmed Disabled in win-x64 Profile

**Author:** Linus (Backend)  
**Date:** 2026-03-14  
**Scope:** Build / Publish

### Context

Created publish profile `src/FinaryExport/Properties/PublishProfiles/win-x64.pubxml` targeting self-contained single-file deployment. Evaluated whether `PublishTrimmed=true` is safe.

### Analysis

Trimming is **not safe** for this project. Three blockers:

1. **System.Text.Json without source generators** — `FinaryApiClient` uses `JsonSerializer.Deserialize<T>(body, _jsonOptions)` with reflection-based polymorphic deserialization. Runtime `JsonSerializerOptions` with `SnakeCaseLower` naming policy. No `JsonSerializerContext` or `[JsonSerializable]` attributes. The trimmer would strip type metadata needed for deserialization. `EncryptedFileSessionStore` also uses reflection-based STJ.

2. **ClosedXML** — uses reflection heavily for cell value handling and type conversion. Not annotated for trimming.

3. **Microsoft.Extensions.Hosting / DI** — generic host uses reflection for service resolution, configuration binding, and logging provider registration. While .NET 10 has improved trim annotations, the combination with CurlImpersonate's native interop adds risk.

### Decision

`PublishTrimmed=false`. The exe is ~90 MB without trimming — acceptable for a desktop CLI tool.

### Future Path

To enable trimming later:
1. Add STJ source generators (`JsonSerializerContext` with `[JsonSerializable]` for all API model types)
2. Verify ClosedXML trim compatibility (or add `[DynamicDependency]` annotations)
3. Test with `<PublishTrimmed>true</PublishTrimmed>` + `<TrimmerSingleWarn>false</TrimmerSingleWarn>` to surface all warnings
4. Consider Native AOT at that point too (same prerequisites)
