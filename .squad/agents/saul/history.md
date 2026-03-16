# Saul — History

## Project Context

- **Project:** FinaryExport — .NET 10 tool that exports data from Finary (wealth management platform) to xlsx files. Reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously.
- **Stack:** .NET 10, C#, CurlImpersonate (TLS bypass), ClosedXML (xlsx), generic host
- **Owner:** the user
- **My role:** MCP Specialist — implement the MCP server that exposes Finary API operations as tools for LLMs
- **Key context:** Auth uses Clerk with CurlImpersonate for TLS fingerprint bypass. API client is `IFinaryApiClient`. Rate limiter at 5 req/s. Session persistence via `ISessionStore`.

## Learnings

### 2026-03-17: MCP Server v1 Implementation

**What was built:**
- `src/FinaryExport.Mcp/` — complete MCP server project with 15 tools across 7 tool classes
- Transport: stdio (standard for VS Code Copilot, Claude Desktop, etc.)
- Auth: session-only — relies on `session.dat` from CLI. `McpCredentialPrompt` throws if no session exists (per user directive — no env var creds, no Otp.NET)
- SDK: `ModelContextProtocol` 1.1.0 (official .NET MCP SDK)

**Key files:**
- `src/FinaryExport.Mcp/FinaryExport.Mcp.csproj` — Exe, RootNamespace=FinaryExport.Mcp, UserSecretsId=finary-mcp-secrets
- `src/FinaryExport.Mcp/Program.cs` — `Host.CreateApplicationBuilder` + `AddFinaryCore()` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`
- `src/FinaryExport.Mcp/McpCredentialPrompt.cs` — safety net, throws `InvalidOperationException`
- `src/FinaryExport.Mcp/Tools/` — 7 tool classes: UserTools, PortfolioTools, AccountTools, TransactionTools, DividendTools, HoldingsTools, AllocationTools

**Architecture patterns:**
- Console logging redirected to stderr via `ConsoleLoggerOptions.LogToStandardErrorThreshold = LogLevel.Trace` (stdout reserved for MCP protocol)
- Tool classes use primary constructor injection with `IFinaryApiClient`
- Category parsing shared via `AccountTools.ParseCategory()` internal static method
- `get_all_accounts` and `get_all_transactions` aggregate across categories with per-category error isolation
- `get_transactions` validates `HasTransactions()` before calling API to give clear error messages
- Tool names are snake_case, descriptions are LLM-facing
- `[McpServerTool(Name = "...")]` with `[Description]` attributes on methods and parameters

**Decisions:**
- No `EnvCredentialPrompt` — per copilot directive, session-only auth
- `ISessionStore` already registered in `AddFinaryCore()` — no duplicate registration needed
- appsettings.json is minimal (just Finary.Period and Logging config — no OutputPath since MCP doesn't export files)

### 2026-03-17: Auto-Init Organization Context for MCP

**Problem:** MCP users had to manually call `get_profiles` then `set_active_profile` before any data tool worked. `IFinaryApiClient` requires `_orgId` and `_membershipId` to be set, but the MCP server never initialized them automatically (unlike the CLI which does it explicitly).

**Solution:** Created `AutoInitFinaryApiClient` decorator (Approach B from the problem analysis):
- Wraps `FinaryApiClient` (concrete), implements `IFinaryApiClient`
- On first call to any data method, auto-calls `GetOrganizationContextAsync()` (resolves owner's default profile)
- Thread-safe via `SemaphoreSlim` + double-checked locking with `volatile bool _initialized`
- Passthrough methods (no auto-init): `GetCurrentUserAsync`, `GetAllProfilesAsync`, `GetOrganizationContextAsync`, `SetOrganizationContext`
- `SetOrganizationContext` and `GetOrganizationContextAsync` both set `_initialized = true` — if user explicitly picks a profile, auto-init is skipped

**DI wiring in Program.cs:**
- Remove the raw `IFinaryApiClient → FinaryApiClient` descriptor from `AddFinaryCore()`
- Register `FinaryApiClient` as concrete singleton
- Register `IFinaryApiClient → AutoInitFinaryApiClient` (takes concrete `FinaryApiClient` in constructor, avoids circular DI)
- "Last registration wins" not needed — we explicitly remove the old descriptor for clarity

**Key file:** `src/FinaryExport.Mcp/AutoInitFinaryApiClient.cs`

### 2026-03-17: MCP Elicitation for Credential Prompt

**Problem:** `McpCredentialPrompt` threw `InvalidOperationException` when no `session.dat` existed, requiring users to run the CLI first. This blocked cold-start auth from the MCP server entirely.

**Solution:** Replaced the throw-only implementation with MCP Elicitation (`McpServer.ElicitAsync`). When no session exists and cold auth triggers, the MCP server now interactively prompts the user for email, password, and TOTP code via the MCP client (VS Code Copilot, Claude Desktop, etc.).

**Key details:**
- `McpServer` (concrete class, no `IMcpServer` interface) injected via primary constructor — the SDK registers it in DI automatically via `AddMcpServer()`
- `ICredentialPrompt.PromptCredentials()` is sync; `ElicitAsync` is async. Bridged with `.GetAwaiter().GetResult()` — safe because the generic host has no `SynchronizationContext`
- Schema uses `ElicitRequestParams.StringSchema` with `Format = "email"` / `Format = "password"` for email/password fields, and `MinLength = 6` / `MaxLength = 6` for TOTP
- All three fields marked as `Required` in the schema
- If user cancels/declines, throws `InvalidOperationException` with clear message
- `ElicitResult.Content` is `IDictionary<string, JsonElement>` — values extracted with `.GetString()`

**SDK learnings:**
- `ModelContextProtocol.Protocol.ElicitRequestParams` has nested types: `RequestSchema`, `StringSchema`, `BooleanSchema`, `NumberSchema`, `PrimitiveSchemaDefinition` (abstract base)
- `ElicitResult.IsAccepted` (bool) + `ElicitResult.Content` (dict) — check both
- No `IMcpServer` interface exists in SDK 1.1.0 — inject concrete `McpServer` directly
- `McpServer.ElicitAsync` has two overloads: non-generic (manual schema) and generic `ElicitAsync<T>` (auto-schema from type)

**Key file:** `src/FinaryExport.Mcp/McpCredentialPrompt.cs`

### 2026-03-17: Async Credential Prompting & Unified Auth Flow

**Problem:** MCP auth was failing because `ICredentialPrompt.PromptCredentials()` was sync but `McpCredentialPrompt` needed async `ElicitAsync()`. Sync-over-async wrapper (`GetAwaiter().GetResult()`) risked deadlocks. Additionally, `GetAllProfilesAsync()` and `GetCurrentUserAsync()` in `AutoInitFinaryApiClient` bypassed `EnsureInitializedAsync()`, causing auth to fail with generic errors when these were called first.

**Solution:**
1. **Made `ICredentialPrompt` async** — `PromptCredentialsAsync(CancellationToken)` replaces `PromptCredentials()`
2. **Updated all implementations:**
   - `ClerkAuthClient.ColdStartAsync()` — now awaits `credentialPrompt.PromptCredentialsAsync(ct)`
   - `ConsoleCredentialPrompt` — wraps sync console I/O with `Task.FromResult`
   - `McpCredentialPrompt` — removed sync-over-async wrapper, method now properly async
3. **Added error handling to `McpCredentialPrompt`** — try/catch around `ElicitAsync` with clear error message: "Run CLI first to create session.dat, or ensure MCP client supports elicitation capability"
4. **Fixed `AutoInitFinaryApiClient`** — ALL methods now call `EnsureInitializedAsync()` before delegating, including `GetAllProfilesAsync()`, `GetCurrentUserAsync()`, and `GetOrganizationContextAsync()`. This ensures auth is triggered regardless of which tool is called first.
5. **Updated test** — `ClerkAuthClientSkipTests` mock now sets up `PromptCredentialsAsync`

**Build result:** 0 errors, 0 warnings, 240/240 tests passing.

**Key learnings:**
- Async all the way down — don't sync-over-async in shared interfaces, even if one implementation (console) is inherently sync
- Decorator pattern: ensure ALL methods that might trigger auth are wrapped, not just data methods
- Error handling at auth boundaries should guide users to action (e.g., "run CLI first" vs generic "auth failed")
- `CancellationToken` parameter allows async methods to respect cancellation throughout the stack

**Key files:** `src/FinaryExport.Core/Auth/ICredentialPrompt.cs`, `src/FinaryExport.Mcp/McpCredentialPrompt.cs`, `src/FinaryExport.Mcp/AutoInitFinaryApiClient.cs`

### 2026-03-18: Elicitation Pre-Flight Capability Check Fix

**Problem:** When no `session.dat` exists, `McpCredentialPrompt.PromptCredentialsAsync` called `mcpServer.ElicitAsync()`, which failed with the SDK's raw `InvalidOperationException("Client does not support elicitation requests.")` — a cryptic error with no guidance.

**Root causes found:**
1. **No pre-flight check** — the code called `ElicitAsync` blindly without checking `ClientCapabilities.Elicitation.Form` first, letting the SDK throw an unhelpful error.
2. **Broken exception filter** — `catch (Exception ex) when (ex is not InvalidOperationException)` intentionally excluded `InvalidOperationException`, but that's exactly what the SDK throws for unsupported capabilities. The user's guidance message was never shown.

**SDK analysis (ModelContextProtocol 1.1.0):**
- `ElicitAsync` calls `ThrowIfElicitationUnsupported(requestParams)` before sending the JSON-RPC request
- That method checks: `ClientCapabilities.Elicitation` (not null?) → `Mode == "form"` + `RequestedSchema` (not null?) + `elicitationCapability.Form` (not null?)
- `ElicitRequestParams.Mode` defaults to `"form"` in 1.1.0
- Server capabilities don't need to declare elicitation — it's a client-advertised capability
- The client sends `capabilities: { elicitation: { form: {} } }` in `initialize` if it supports form-mode elicitation
- `McpServer.ClientCapabilities` is populated after the initialization handshake

**Fix applied:**
1. Added `EnsureElicitationSupported()` pre-flight check that inspects `mcpServer.ClientCapabilities?.Elicitation?.Form` before calling `ElicitAsync`
2. Added secondary `catch (InvalidOperationException) when (!IsElicitationSupported())` to handle the edge case where capabilities change after the pre-flight check
3. Error message includes actionable workaround: "run the FinaryExport CLI first to create session.dat"
4. Kept the generic `catch (Exception ex) when (ex is not InvalidOperationException)` for non-capability errors (network issues, serialization failures, etc.)

**Key learnings:**
- MCP elicitation is a **client-side capability** — the server doesn't declare it, the client advertises it during `initialize`
- In SDK 1.1.0: `ElicitRequestParams.Mode` defaults to `"form"`, `ClientCapabilities.Elicitation` has `.Form` and `.Url` sub-capabilities
- The `ThrowIfElicitationUnsupported` method is called inside `ElicitAsync` and throws `InvalidOperationException` — always pre-check before calling
- Exception filter `when (ex is not X)` can silently let critical exceptions propagate — be careful with filter conditions

**Key file:** `src/FinaryExport.Mcp/McpCredentialPrompt.cs`
