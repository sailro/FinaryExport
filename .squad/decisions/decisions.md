# Team Decisions

## Decision: Use Claude Opus 4.6 (1M context) for All Agent Spawns

**By:** the user (via Copilot directive)  
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

## Decision: Auto-Init Organization Context in MCP Server

**Author:** Saul (MCP Specialist)  
**Date:** 2026-03-17  
**Scope:** MCP Server / DI / API Client

### Context

MCP users had to manually call `get_profiles` → `set_active_profile` before any data tool would work. This was bad UX — new users would get cryptic failures on their first tool call because `_orgId` and `_membershipId` were null.

### Decision

Created `AutoInitFinaryApiClient` — a decorator over `FinaryApiClient` that lazily auto-initializes the organization context on the first data call. Uses `SemaphoreSlim` for thread-safe one-time init.

**Approach chosen:** Decorator pattern (Approach B) — transparent to tool classes, no duplication, no startup delay.

**Rejected alternatives:**
- Approach A (lazy init in each tool method) — too much duplication across 7 tool classes
- Approach C (init at startup) — some MCP hosts timeout on slow server starts

### Impact

- MCP data tools now work immediately without any manual setup calls
- Users can still explicitly call `set_active_profile` to switch profiles (this skips auto-init)
- No changes to `IFinaryApiClient`, `FinaryApiClient`, or any tool class
- Build: 0 errors, 0 warnings. Tests: 240/240 passing.

## Decision: Elicitation Pre-Flight Capability Check

**Author:** Saul (MCP Specialist)
**Date:** 2026-03-18
**Scope:** MCP Server / Auth

### Context

The MCP elicitation flow was broken. When no `session.dat` exists and a tool is invoked, the auth chain calls `McpCredentialPrompt.PromptCredentialsAsync`, which calls `mcpServer.ElicitAsync(...)`. The SDK's internal `ThrowIfElicitationUnsupported` method throws `InvalidOperationException("Client does not support elicitation requests.")` if the MCP host didn't advertise `elicitation.form` capability during initialization.

Two bugs compounded the issue:
1. No pre-flight check before calling `ElicitAsync`
2. The `catch` filter `when (ex is not InvalidOperationException)` excluded exactly the exception type the SDK throws, so the helpful guidance message was never shown

### Decision

1. **Always pre-check `mcpServer.ClientCapabilities?.Elicitation?.Form` before calling `ElicitAsync`** — fail fast with a clear actionable error message including the workaround (run CLI first).
2. **Add a secondary catch for the SDK's `InvalidOperationException`** as a safety net.
3. **Error messages always include the workaround** — "run the FinaryExport CLI first to create session.dat".

### Key Findings

- **Elicitation is a client-side capability.** The server doesn't declare it. The client sends `capabilities: { elicitation: { form: {} } }` during `initialize`.
- **In SDK 1.1.0:** `ElicitRequestParams.Mode` defaults to `"form"`. `ElicitationCapability` has `.Form` and `.Url` sub-capabilities.
- **GitHub Copilot CLI reportedly supports elicitation** (form mode), but support depends on the client version.
- **No server-side configuration needed** — `ServerCapabilities` doesn't have an elicitation property.

### Impact

- `McpCredentialPrompt.cs` is the only file changed
- Build: 0 C# errors, 0 warnings
- Tests: 240/240 passing

## Decision: Backfill ElicitationCapability.Form for Spec-Lagging Clients

**Author:** Saul (MCP Specialist)  
**Date:** 2026-03-18  
**Scope:** MCP Server — Elicitation Capability Negotiation

### Context

SDK 1.1.0 (protocol 2025-11-25) requires `ElicitationCapability.Form` to be non-null for form-mode elicitation. The Copilot CLI sends `elicitation: {}` during initialization without the `form` sub-capability introduced in the 2025-06-18 spec revision. This caused `EnsureElicitationSupported()` and the SDK's `ThrowIfElicitationUnsupported()` to reject elicitation attempts.

### Decision

Instead of strictly checking `Elicitation?.Form is not null`, we now:
1. Check only `Elicitation is not null` (client supports elicitation in general)
2. Backfill `Form` with `new FormElicitationCapability()` if missing

This tolerates clients implementing older MCP spec versions while satisfying the SDK's stricter requirements.

### Rationale

- `FormElicitationCapability` is an empty marker class — backfilling it adds no semantic meaning, just satisfies the SDK guard
- `ClientCapabilities.Elicitation.Form` is writable — safe to mutate in-place
- Alternative approaches (message filters, reflection on private fields, `KnownClientCapabilities`) were all more complex and fragile
- `KnownClientCapabilities` was investigated but gets **overwritten** (not merged) during `initialize` — useless here

### Impact

- Any MCP client that sends `elicitation: {}` (without `form`/`url` sub-properties) will now work
- If a client sends neither `elicitation` nor anything, the error message is still clear and actionable
- No risk: the worst case is backfilling `Form` when the client already has it (the `??=` makes this a no-op)

## Decision: Async Credential Prompting (MCP Auth Fix)

**Author:** Saul (MCP Specialist)  
**Date:** 2026-03-17  
**Scope:** Auth / MCP  
**Status:** ✅ Implemented

### Problem

MCP tools were failing with generic error `MCP server 'finary': An error occurred invoking 'get_profiles'` when called. Root causes:

1. **Sync-over-async in credential prompt:** `ICredentialPrompt.PromptCredentials()` was synchronous, but `McpCredentialPrompt` needed to call async `McpServer.ElicitAsync()`. The sync-over-async wrapper (`GetAwaiter().GetResult()`) could deadlock or fail in some execution contexts.

2. **Incomplete auto-init coverage:** `AutoInitFinaryApiClient.GetAllProfilesAsync()` and `GetCurrentUserAsync()` bypassed `EnsureInitializedAsync()`, meaning if these were called first (before any data method), auth never triggered and tools failed with generic errors.

3. **No elicitation error handling:** If `ElicitAsync` failed (client doesn't support elicitation, network issue, etc.), users saw a raw exception with no guidance.

### Decision

**Make `ICredentialPrompt` async and wrap ALL `AutoInitFinaryApiClient` methods with auth initialization.**

#### Changes

1. **`ICredentialPrompt` interface** — signature changed to `Task<(string, string, string)> PromptCredentialsAsync(CancellationToken ct = default)`

2. **`ClerkAuthClient.ColdStartAsync()`** — updated to `await credentialPrompt.PromptCredentialsAsync(ct)`

3. **`ConsoleCredentialPrompt`** — implements async interface with `Task.FromResult` wrapper (Console I/O is inherently sync)

4. **`McpCredentialPrompt`** — removed sync-over-async wrapper, made method properly async:
   ```csharp
   public async Task<...> PromptCredentialsAsync(CancellationToken ct)
   {
       try {
           var result = await mcpServer.ElicitAsync(requestParams, ct);
           // ... extract fields
       }
       catch (Exception ex) when (ex is not InvalidOperationException) {
           throw new InvalidOperationException(
               "Finary authentication required but credential prompting failed. " +
               "Run the FinaryExport CLI first to create a session (session.dat), " +
               "or ensure your MCP client supports the 'elicitation' capability. " +
               $"Details: {ex.Message}", ex);
       }
   }
   ```

5. **`AutoInitFinaryApiClient`** — ALL methods now call `EnsureInitializedAsync()` before delegating:
    - `GetAllProfilesAsync()` ✅
    - `GetCurrentUserAsync()` ✅
    - `GetOrganizationContextAsync()` ✅
    - All data methods (already wrapped) ✅
    - `SetOrganizationContext()` — sync method, marks `_initialized = true` (unchanged)

6. **Test update** — `ClerkAuthClientSkipTests` mock now sets up `PromptCredentialsAsync` instead of `PromptCredentials`

### Rationale

- **Async all the way:** Shared interfaces should be async if any implementation needs async. Don't sync-over-async in Core — it risks deadlocks and blocks threads unnecessarily.
- **Complete decorator coverage:** If any method might trigger auth (directly or indirectly), wrap it. `GetAllProfilesAsync()` and `GetCurrentUserAsync()` are valid entry points for MCP tools.
- **User-facing error messages:** Auth failures should guide users to action ("run CLI first" or "check MCP client capabilities") not dump stack traces.

### Impact

- **Positive:** MCP auth now works reliably with elicitation. Clear error messages when elicitation fails. No sync-over-async risk.
- **Breaking:** `ICredentialPrompt` signature changed — any external implementations (none exist) would need updates.
- **Testing:** 240/240 tests passing. Zero errors, zero warnings.

### Alternatives Considered

1. **Keep interface sync, fix sync-over-async in `McpCredentialPrompt`:**  
   Not viable — `ElicitAsync` is fundamentally async (MCP protocol), can't make it sync without blocking.

2. **Two interfaces: `ICredentialPrompt` (sync) and `IAsyncCredentialPrompt` (async):**  
   Complexity for no gain. Console I/O wraps trivially with `Task.FromResult`. Better to have one async interface.

3. **Only wrap data methods in `AutoInitFinaryApiClient`:**  
   Doesn't solve the problem — `get_profiles` tool calls `GetAllProfilesAsync()`, which needs auth.

### Implementation

- Build: 0 errors, 0 warnings
- Tests: 240/240 passing
- Files changed: 6 (ICredentialPrompt.cs, ClerkAuthClient.cs, ConsoleCredentialPrompt.cs, McpCredentialPrompt.cs, AutoInitFinaryApiClient.cs, ClerkAuthClientSkipTests.cs)

## Decision: MCP Elicitation for Cold-Start Auth

**Author:** Saul (MCP Specialist)  
**Date:** 2026-03-17  
**Scope:** MCP Server — Authentication

### Decision

Replaced the throw-only `McpCredentialPrompt` with an interactive implementation using MCP Elicitation (`McpServer.ElicitAsync`). The MCP server can now perform cold-start authentication by prompting the user for credentials via the MCP client.

### Context

Previously, per user directive, the MCP server could only work with an existing `session.dat` from the CLI. This meant users had to run the CLI exporter first. The new implementation removes that requirement — if no session exists, the MCP server prompts for email, password, and TOTP code interactively.

### Technical Notes

- `ICredentialPrompt.PromptCredentialsAsync()` is async and `ElicitAsync` is async. No bridging issues.
- `McpServer` is a concrete class (no `IMcpServer` interface in SDK 1.1.0). Injected directly via DI.
- This supersedes the earlier "session-only auth" directive for MCP. The MCP server can now independently authenticate.

### Impact

- MCP users no longer need to run the CLI first
- `session.dat` warm-start still works as before (preferred path)
- Cold auth only triggers if no valid session exists
- MCP clients that don't support elicitation will get an error with clear guidance (elicitation is MCP spec 2025-06-18+)

## Decision: MCP Tool Surface — Read-Only Phase 1

**Author:** Livingston (Protocol Analyst)
**Date:** 2026-03-14
**Scope:** MCP Server / API Surface

### Context

Cataloged the complete API surface for MCP tool exposure. Analyzed all 15 methods on `IFinaryApiClient`, 37 model types, and ~20 additional wire-observed endpoints.

### Decision

**Phase 1 should expose all 15 `IFinaryApiClient` methods as MCP tools.** Every one is read-only (GET). Zero mutations exist in the client.

The only wire-observed mutation (`PUT /users/me/ui_configuration`) must NOT be exposed.

### Key Constraints for Implementation

1. **Bootstrap required:** MCP session must call `finary_get_org_context` before any data tool — org context is required state.
2. **Transaction category restriction:** Only 4 of 10 `AssetCategory` values support transactions. Tool description must document this.
3. **Pagination is internal:** `GetPaginatedListAsync` handles pagination transparently. MCP tools return full lists.
4. **Rate limiting is infrastructure:** Already handled by `RateLimiter` + `FinaryDelegatingHandler`. No MCP-layer rate limiting needed.

### Artifact

Full catalog: `.squad/artifacts/mcp-tool-catalog.md`

## Decision: User Directive — MCP Auth via Shared Session

**By:** the user (via Copilot)
**Date:** 2026-03-16T08:45Z
**Scope:** MCP Server Authentication

### Directive

The MCP server must reuse the session.dat created by the CLI exporter. No cold auth / no env var credentials / no TOTP in the MCP server. If no session.dat exists, explain that a first export run is needed to initialize it.

### Rationale

User request — simplifies MCP auth, removes Otp.NET dependency, single auth source via CLI.

### Status

⚠️ **SUPERSEDED** (2026-03-18) — Saul's elicitation implementation now supports cold-start auth in MCP. The MCP server can authenticate independently without requiring CLI to run first. Session.dat warm-start still works as backup. See "MCP Elicitation for Cold-Start Auth" decision.

## Decision: MCP Architecture Proposal — Extract Shared Library

**Author:** Rusty (Lead)  
**Date:** 2026-03-17  
**Status:** ✅ Implemented  
**Scope:** Solution structure, shared code extraction

### Problem Statement

We have a working CLI tool (`FinaryExport`) that authenticates with Finary via Clerk, queries all API endpoints, and exports to Excel. The request is to expose these same Finary API operations as MCP (Model Context Protocol) tools so an LLM can interact with the Finary API directly — querying accounts, portfolio, transactions, etc.

The constraint: reuse existing auth, API client, models, and infrastructure. Don't rewrite what works.

### Decision: Extract Shared Library

**Option A:** Project references only (MCP project references CLI project) — **Rejected.** The CLI project has `<OutputType>Exe</OutputType>`, `ConsoleCredentialPrompt`, System.CommandLine, ClosedXML, and export-specific code. Referencing it drags all that in. Coupling is wrong.

**Option B:** Extract shared code into `FinaryExport.Core` class library — **✅ Selected.**

#### Rationale

- Clean dependency direction: both `FinaryExport` (CLI) and `FinaryExport.Mcp` (server) reference `FinaryExport.Core`
- Core contains: API client, auth abstractions, models, infrastructure (rate limiter, handlers, curl bridge)
- CLI keeps: System.CommandLine, ClosedXML, export sheets, console prompts, Program.cs
- MCP keeps: MCP SDK, tool definitions, MCP-specific credential handling, server entry point
- The `ICredentialPrompt` interface stays in Core. `ConsoleCredentialPrompt` moves to CLI. MCP provides its own implementation (now: elicitation-based, can fallback to env-var or session.dat)

### Solution Structure (After Extraction)

```
FinaryExport.slnx
├── src/
│   ├── FinaryExport.Core/                    # Shared library
│   │   ├── Api/, Auth/, Configuration/, Infrastructure/, Models/
│   ├── FinaryExport/                         # CLI tool
│   │   ├── Program.cs, ConsoleCredentialPrompt, Export/
│   ├── FinaryExport.Mcp/                     # MCP server
│   │   ├── Program.cs, McpCredentialPrompt, Tools/
│   └── FinaryExport.Tests/
└── FinaryExport.slnx
```

### Key Design Decisions

- **RootNamespace:** Core uses `FinaryExport` — all namespaces unchanged, mechanical file moves only
- **ServiceCollectionExtensions split:**
  - Core: `AddFinaryCore()` — registers API client, auth, token refresh, rate limiter
  - CLI: registers Core + `ConsoleCredentialPrompt` + export services
  - MCP: registers Core + `McpCredentialPrompt` + auto-init decorator
- **MCP transport:** stdio (standard for MCP clients like VS Code, Claude Desktop)
- **MCP credential sources:** Elicitation (primary), session.dat (warm start), environment variables (fallback)

### MCP Server Design

15 read-only tools exposed from `IFinaryApiClient` (all GET endpoints, zero mutations):
- 3 Portfolio tools
- 3 Account tools
- 2 Transaction tools
- 1 Dividend tool
- 2 Holdings tools
- 2 Allocation tools
- 2 User/Profile tools

All tools auto-initialize org context on first call (transparent, no manual setup required).

### Implementation Status

✅ Core extraction complete  
✅ CLI references Core, ConsoleCredentialPrompt moved  
✅ MCP project created with full tool surface  
✅ MCP auth (elicitation + auto-init) working  
✅ Build: 0 errors, 0 warnings  
✅ Tests: 240/240 passing  

### Impact

- No behavior change in CLI (just reorganized)
- MCP server now available as LLM tool host
- Shared library (Core) can be reused by other projects
- CI/CD can test Core in isolation

### Artifacts

Full proposal: `.squad/decisions/inbox/rusty-mcp-architecture.md`

## Security Audit Findings — 2026-03-18

**Author:** Livingston (Protocol Analyst)
**Scope:** Full repo security audit — git history, source code, config, squad files, credential handling

### Findings

#### 🟢 OK — No Secrets in Code or Git History

Scanned 40+ commits across all branches for: password, secret, token, apikey, api_key, bearer, cookie, session, credential, totp, email addresses. Zero real credentials found anywhere in tracked files or git history.

#### 🟢 OK — Test Data is Synthetic

All test fixtures use synthetic identifiers: "Jean Dupont", "Marie Dupont", `test@example.com`, `user@finary.com`, JWTs with `fake_signature`. Compliant with D-pii.

#### 🟢 OK — Configuration Files Clean

Both `appsettings.json` files (CLI + MCP) contain only logging levels and period config. No secrets, no connection strings, no embedded tokens. `global.json` has only test runner config. No `.env` files exist.

#### 🟢 OK — Session Store Secure

`EncryptedFileSessionStore` stores to `~/.finaryexport/session.dat` (outside repo). DPAPI encryption with `DataProtectionScope.CurrentUser`. Path gitignored. Failures are non-fatal.

#### 🟢 OK — Credential Handling Memory-Only

- `ConsoleCredentialPrompt`: password masked with `*`, credentials returned as in-memory tuple, never logged/stored.
- `McpCredentialPrompt`: credentials extracted from elicitation result, returned as in-memory tuple, never logged/stored.
- `ClerkAuthClient`: session IDs truncated to 12 chars in log output via `TruncateId()`. No credential values logged anywhere.

#### 🟢 OK — gitignore Coverage Complete

Verified: `session.dat`, `*.xlsx`, `*.har`, `state.json`, `.env`, `log.txt`, `appsettings.*.json` all gitignored. `git ls-files` confirms zero sensitive files tracked.

#### 🟡 WARNING — D-pii Violations Fixed

Found and fixed 3 instances of real name in tracked squad files:
1. `.squad/agents/saul/history.md:7` — "**Owner:** the user" → "the user"
2. `.squad/decisions.md:252` — "**By:** the user" → "the user"
3. `.squad/decisions/decisions.md:5` — "**By:** Sebastien" → "the user"

These violated D-pii ("squad files refer to 'the user' — never real names"). All three have been corrected.

#### 🟡 NOTE — Git Author Email

`sebastien@lebreton.fr` appears in all git commit author metadata. This is standard git behavior and cannot be changed without history rewriting. If the repo goes public, consider whether this is acceptable or if `git filter-repo` should anonymize commit authors.

### Recommendation

No action required beyond the D-pii fixes already applied. The codebase has strong security hygiene. The previous audit's recommendations (gitignore for log.txt and .env) are still in place and working.
