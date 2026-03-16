# Team Decisions

## Decision: Use Claude Opus 4.6 (1M context) for All Agent Spawns

**By:** Sebastien (via Copilot directive)  
**Date:** 2026-03-15T18:36Z  
**Scope:** Tool configuration, agent spawning

### Summary

All agent spawns this session and going forward use Claude Opus 4.6 (1M context) — model ID `claude-opus-4.6-1m`. This is the exact model the user runs in the foreground.

### Rationale

User request — desires the latest premium model for all work, not older versions. Ensures consistency between foreground and background agents.

### Impact

- Agent configuration: All spawns pass `model: "claude-opus-4.6-1m"` parameter
- No change to agent responsibilities or workflows
- Improves reasoning quality and context handling for complex tasks

## Decision: Transaction Categories in Export

**Author:** Linus (Backend)  
**Date:** 2026-03-15  
**Scope:** Models / API / Export

### Summary

Added `GetTransactionCategoriesAsync` to `IFinaryApiClient` and enriched the Transactions sheet with a "Transaction Category" column (column J). Categories are fetched once per export, flattened from their hierarchical structure into a flat `Dictionary<int, string>` lookup, and resolved per-transaction via `ExternalIdCategory`.

### Key Details

- **New model:** `TransactionCategory` record in `Models/Transactions/` — mirrors the API shape including recursive `Subcategories` list.
- **API endpoint:** `GET {BasePath}/transaction_categories?included_in_analysis=true` — uses the standard `FinaryResponse<T>` envelope unwrapped by `GetAsync<T>`.
- **UnifiedFinaryApiClient:** Simple passthrough with `UseOwnerContext()` — transaction categories are org-level, not membership-specific.
- **Column placement:** Column J ("Transaction Category") after Commission (I). Column A remains the asset category enum display name.

### Impact

- New column appears in all exported xlsx files. If a transaction has no `ExternalIdCategory`, the cell is left blank.
- If Finary adds new top-level categories, they'll be picked up automatically since the lookup is built dynamically from the API response.
- The `TransactionCategory` model includes all fields from the API even though only `id` and `name` are currently used — supports future features.

## Clerk Auth Flow & API Protocol Details

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

## Decision: MCP Elicitation Credential Prompt — Root Cause Fix

**Author:** Saul + Scribe (Debugging Session)  
**Date:** 2026-03-18  
**Scope:** MCP Server — Authentication / Elicitation  
**Commit:** 5d2722a "fix: MCP elicitation credential prompt"

### Problem Statement

MCP server's credential elicitation was completely broken. All tool calls requiring authentication returned "An error occurred" with no diagnostic details. Users could not authenticate via MCP tools. The root cause was discovered after three earlier Saul spawns added robustness (pre-flight checks, capability backfill) but not the fix.

### Root Cause

`Format = "password"` in `ElicitRequestParams.StringSchema` violates MCP SDK constraints. The SDK only accepts these format values in StringSchema:
- `email`
- `uri`
- `date`
- `date-time`

Using any other value (including `"password"`) throws `ArgumentException`. The original code silently swallowed this exception, resulting in generic "An error occurred" error messages that masked the real problem.

### The Fix

Removed `Format = "password"` from the password field schema — a one-line change:

```csharp
// BEFORE: throws ArgumentException during ElicitAsync
StringSchema password = new StringSchema { Format = "password" };

// AFTER: works correctly
StringSchema password = new StringSchema();
```

**Result:** Elicitation now works perfectly. Users can authenticate via MCP tools.

### The Debugging Journey

1. **Initial hypothesis (wrong):** Copilot CLI doesn't support elicitation
   - Disproven: CLI does support elicitation
   
2. **Spawns 1 & 2 improvements:** Added pre-flight capability check + FormElicitationCapability backfill
   - Improved robustness but didn't fix the core issue
   
3. **Breakthrough moment:** Built debug_elicitation tool that called ElicitAsync directly
   - **It worked!** Users could authenticate through it
   - Revealed paradox: "Why does debug work but real tools fail?"
   
4. **Exception visibility:** Wrapped get_profiles in try/catch
   - Caught the real exception: `ArgumentException("Invalid Format value in StringSchema")`
   - Identified the constraint violation
   
5. **Root cause identified:** MCP SDK validates Format values strictly
   - Checked SDK source and docs
   - Found the allowed values list
   - Removed Format="password"
   - Elicitation now works

### Key Decisions

1. **MCP Elicitation works with Copilot CLI** — the server must use only valid format values (email, uri, date, date-time) in StringSchema

2. **FormElicitationCapability backfill is kept** — some clients send `elicitation: {}` without form sub-mode. This safeguard remains necessary.

3. **Original "session-only auth" directive is SUPERSEDED** — The MCP server successfully supports cold authentication via elicitation when no session.dat exists (assuming a valid MCP client).

4. **Exception visibility is critical for debugging** — Silent exception swallowing masked the real problem. The breakthrough came from surfacing the exception.

### Changes Made

- Removed `Format = "password"` from ElicitRequestParams.StringSchema
- Removed temporary debug_elicitation tool (used only to validate hypothesis)
- Cleaned up exception wrapping and verbose logging (exception visibility was only needed for root cause discovery)
- Kept FormElicitationCapability backfill from earlier Spawns 1-2 (necessary for client compatibility)

### Lessons Recorded

1. **MCP SDK StringSchema Format is strictly validated** — only email, uri, date, date-time are allowed. Always check SDK documentation for constrained fields.

2. **Debug tools that directly test hypotheses are powerful** — The debug_elicitation tool proved capability exists and led to asking "why does this work but that doesn't?"

3. **Paradoxes point to root cause** — "Why does debug work but real tools fail?" is a powerful debugging question that led directly to the fix.

4. **Silent exceptions are dangerous** — Always surface exceptions during investigation, even if the final solution removes the verbose error handling.

5. **Exception filters can hide critical errors** — The original `catch (Exception ex) when (ex is not InvalidOperationException)` excluded the exact exception type the SDK was throwing.

### Impact

- ✅ Credential elicitation works
- ✅ Users can authenticate via MCP tools without running CLI first
- ✅ All auth-dependent features now accessible
- ✅ Error messages are clear (no more generic "An error occurred")
- ✅ MCP server achieves true cold-start capability

### Related Decisions

- **Supersedes:** D-mcp-complete decision #5 ("session-only auth") — MCP now supports cold auth
- **Builds on:** Decision: Elicitation Pre-Flight Capability Check
- **Builds on:** Decision: Backfill ElicitationCapability.Form for Spec-Lagging Clients
