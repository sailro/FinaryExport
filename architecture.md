# FinaryExport — Architecture Document

> **Author:** Rusty (Lead)
> **Revised:** 2026-03-17 — updated for MCP server, Core extraction, elicitation auth
> **Status:** Living document tracking current implementation state

---

## Table of Contents

1. [Overview](#1-overview)
2. [Solution Structure](#2-solution-structure)
3. [Dependencies](#3-dependencies)
4. [CLI Layer](#4-cli-layer)
5. [Authentication](#5-authentication)
6. [API Layer](#6-api-layer)
7. [Multi-Profile & Unified Export](#7-multi-profile--unified-export)
8. [Export Pipeline](#8-export-pipeline)
9. [MCP Server](#9-mcp-server)
10. [Configuration](#10-configuration)
11. [Infrastructure](#11-infrastructure)
12. [Data Model Summary](#12-data-model-summary)
13. [Design Decisions](#13-design-decisions)

---

## 1. Overview

FinaryExport is a .NET 10 multi-project solution that exports Finary wealth management data to Excel workbooks and exposes it via an MCP (Model Context Protocol) server for AI assistants. It authenticates via Clerk using TLS-fingerprinted HTTP requests (CurlImpersonate), iterates over all user profiles (memberships), and produces one `.xlsx` per profile plus a unified workbook combining all profiles.

The solution contains three projects:
- **FinaryExport.Core** — shared class library with API client, auth, models, infrastructure
- **FinaryExport** — CLI tool for Excel export
- **FinaryExport.Mcp** — MCP server exposing Finary data to AI assistants (Claude Desktop, VS Code Copilot, etc.)

Key technical choices:
- **CurlImpersonate** — Chrome 136 TLS fingerprint to bypass Cloudflare bot detection
- **Interactive credentials** — email/password/TOTP prompted at runtime (CLI) or via MCP Elicitation (MCP server); no secrets in config
- **DPAPI session persistence** — encrypted cookie/session cache for warm-start auth
- **System.CommandLine** — structured CLI with `export`, `clear-session`, `version` commands
- **ClosedXML** — Excel generation without COM interop
- **ModelContextProtocol** — stdio-based MCP server with 15 read-only tools

---

## 2. Solution Structure

```
FinaryExport.slnx
├── Directory.Build.props                  # Shared: net10.0, nullable, implicit usings
├── Directory.Packages.props               # Central Package Management (all NuGet versions)
│
├── src/FinaryExport.Core/                 # Shared class library (RootNamespace=FinaryExport)
│   ├── FinaryExport.Core.csproj
│   ├── Api/
│   │   ├── IFinaryApiClient.cs            # API contract
│   │   ├── FinaryApiClient.cs             # Partial class: core setup, org context, pagination
│   │   ├── FinaryApiClient.Categories.cs  # Category-generic endpoints
│   │   ├── FinaryApiClient.Portfolio.cs   # Portfolio, timeseries, dividends, allocations, fees, asset list
│   │   ├── FinaryApiClient.Reference.cs   # Holdings accounts
│   │   ├── FinaryApiClient.Transactions.cs # Transaction endpoints (paginated)
│   │   ├── UnifiedFinaryApiClient.cs      # Decorator: merges data across all profiles
│   │   └── RateLimiter.cs                 # Token-bucket, ~5 req/s
│   ├── Auth/
│   │   ├── ClerkAuthClient.cs             # Clerk auth (CurlImpersonate), cold/warm start
│   │   ├── ITokenProvider.cs              # JWT provider contract
│   │   ├── ICredentialPrompt.cs           # Credential input contract
│   │   ├── ISessionStore.cs               # Session persistence contract
│   │   ├── EncryptedFileSessionStore.cs   # DPAPI-encrypted file storage
│   │   ├── SessionData.cs                 # Record: SessionId + Cookies
│   │   └── TokenRefreshService.cs         # IHostedService: refreshes JWT every 50s
│   ├── Configuration/
│   │   └── FinaryOptions.cs               # OutputPath, SessionStorePath
│   ├── Infrastructure/
│   │   ├── ServiceCollectionExtensions.cs  # AddFinaryCore() — DI registration for shared services
│   │   ├── CurlMessageHandler.cs          # Bridges CurlClient to HttpMessageHandler
│   │   ├── FinaryDelegatingHandler.cs     # Auth headers, rate limit, 401/429 retry
│   │   └── CompactConsoleFormatter.cs     # Single-line log formatter
│   └── Models/
│       ├── ApiEnvelope.cs                 # FinaryResponse<T> + FinaryError
│       ├── AssetCategory.cs               # Enum + extension methods (ToUrlSegment, ToDisplayName, HasTransactions)
│       ├── Accounts/                      # Account, HoldingsAccount, OwnershipEntry, SecurityInfo, SecurityPosition
│       ├── Portfolio/                     # PortfolioSummary, TimeseriesData, DividendSummary (incl. DividendAssetInfo), AllocationData, AssetListEntry, FeeSummary
│       ├── Transactions/                  # Transaction, TransactionCategory
│       └── User/                          # FinaryProfile, Membership, Organization, UserProfile, UiConfiguration, DisplayCurrencyInfo
│
├── src/FinaryExport/                      # CLI tool
│   ├── FinaryExport.csproj                # References FinaryExport.Core
│   ├── Program.cs                         # CLI entry point (System.CommandLine)
│   ├── ConsoleCredentialPrompt.cs         # Interactive console prompts for email/password/TOTP
│   ├── appsettings.json
│   └── Export/
│       ├── ExportContext.cs               # Controls display vs raw value selection
│       ├── IWorkbookExporter.cs           # Exporter contract
│       ├── WorkbookExporter.cs            # Iterates ISheetWriter list, saves .xlsx
│       ├── Formatting/
│       │   └── ExcelStyles.cs             # Currency/percent/date formats, header styling
│       └── Sheets/
│           ├── ISheetWriter.cs            # Sheet writer contract
│           ├── PortfolioSummarySheet.cs   # Portfolio value, allocation, performance
│           ├── AccountsSheet.cs           # Accounts across all asset categories
│           ├── TransactionsSheet.cs       # Buy/sell/income/expense records
│           ├── DividendsSheet.cs          # Dividend income
│           └── HoldingsSheet.cs           # Individual security positions
│
├── src/FinaryExport.Mcp/                  # MCP server
│   ├── FinaryExport.Mcp.csproj            # References FinaryExport.Core
│   ├── Program.cs                         # MCP host entry point (stdio transport)
│   ├── McpCredentialPrompt.cs             # MCP Elicitation-based credential prompts
│   ├── AutoInitFinaryApiClient.cs         # Decorator: auto-resolves org context on first call
│   ├── appsettings.json
│   └── Tools/
│       ├── UserTools.cs                   # get_user_profile, get_profiles, set_active_profile
│       ├── PortfolioTools.cs              # get_portfolio_summary, get_portfolio_timeseries, get_portfolio_fees
│       ├── AccountTools.cs                # get_accounts, get_all_accounts, get_category_timeseries
│       ├── TransactionTools.cs            # get_transactions, get_all_transactions
│       ├── DividendTools.cs               # get_dividends
│       ├── HoldingsTools.cs               # get_holdings, get_asset_list
│       └── AllocationTools.cs             # get_geographical_allocation, get_sector_allocation
│
└── src/FinaryExport.Tests/                # Test project
    ├── FinaryExport.Tests.csproj          # References FinaryExport.Core + FinaryExport
    ├── Api/                               # API client tests
    ├── Auth/                              # Auth tests
    ├── Export/                            # Sheet writer tests
    ├── Infrastructure/                    # DI, handler tests
    ├── Fixtures/                          # Test data
    └── Helpers/                           # Test utilities
```

---

## 3. Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| ClosedXML | 0.105.0 | Excel workbook generation |
| Loxifi.CurlImpersonate | 1.1.0 | Chrome 136 TLS fingerprint (Cloudflare bypass) |
| Microsoft.Extensions.Hosting | 10.0.5 | Generic host, DI, logging, hosted services |
| Microsoft.Extensions.Http | 10.0.5 | HttpClientFactory for Finary API |
| ModelContextProtocol | 1.1.0 | MCP server SDK (stdio transport, tool discovery) |
| System.CommandLine | 2.0.5 | CLI argument parsing (SetAction API) |
| System.Security.Cryptography.ProtectedData | 10.0.5 | DPAPI encryption for session store |

**Target framework:** `net10.0`

**Build tooling:** `Directory.Build.props` sets shared properties (target framework, nullable, implicit usings). `Directory.Packages.props` centralizes all NuGet package versions via Central Package Management.

**Test framework:** xUnit v3 (3.2.2) with FluentAssertions 8.8.0 and Moq 4.20.72.

---

## 4. CLI Layer

`Program.cs` uses `System.CommandLine` to define three commands:

### `export` (default)

Options:
- `--output` — Output file path (default: `finary-export.xlsx`)

Flow:
1. Build `IHost` with `ConfigureHost` (binds `FinaryOptions`, registers all services)
2. Resolve `ClerkAuthClient`, call `LoginAsync()` (warm or cold start)
3. Call `GetAllProfilesAsync()` to enumerate memberships
4. Loop over each profile:
   - `SetOrganizationContext(orgId, membershipId)`
   - Detect display currency via `GetCurrentUserAsync()` → `UiConfiguration.DisplayCurrency.Symbol`
   - `ExportAsync(profilePath, api, new ExportContext { UseDisplayValues = true, DisplayCurrencySymbol = symbol })`
5. Create `UnifiedFinaryApiClient` wrapping `IFinaryApiClient`
6. `ExportAsync(unifiedPath, unifiedApi, new ExportContext { UseDisplayValues = false, DisplayCurrencySymbol = symbol })`
7. Stop the host (shuts down `TokenRefreshService`)

### `clear-session`

Resolves `ISessionStore`, calls `ClearSessionAsync()`. Used when the cached session is stale or corrupt.

### `version`

Prints the assembly informational version.

---

## 5. Authentication

### Architecture

Clerk auth uses **CurlImpersonate directly** — not via HttpClientFactory. `ClerkAuthClient` creates its own `CurlClient(BrowserProfile.Chrome136)` instances for each Clerk HTTP call. This is separate from the Finary API HTTP pipeline.

```
ConsoleCredentialPrompt
        |
        v
ClerkAuthClient --> CurlClient(Chrome136) --> clerk.finary.com
   |-- ColdStartAsync()    POST sign_ins -> POST attempt_second_factor -> tokens
   |-- TryWarmStartAsync() restore cookies -> POST tokens
   +-- RefreshTokenAsync() POST tokens with session_id
        |
        v
  EncryptedFileSessionStore (DPAPI)
```

### LoginAsync Flow

1. **Try warm start:** Load `SessionData` from `EncryptedFileSessionStore`, restore cookies into a `CookieContainer`, call `PostTokensAsync`. If it succeeds (valid JWT returned), warm start is complete.
2. **Fall back to cold start:** Prompt user via `ICredentialPrompt` for email, password, and optionally TOTP code. Then:
   - `POST /v1/client/sign_ins` with email + password
   - If 2FA required: `POST /v1/client/sign_ins/{id}/attempt_second_factor` with TOTP code
   - Extract JWT from response, or call `PostTokensAsync` to get it
3. **Save session:** Persist `SessionData(sessionId, cookies)` to encrypted file store.

### Token Refresh

`TokenRefreshService` (IHostedService) runs a `PeriodicTimer` at 50-second intervals. Calls `ClerkAuthClient.RefreshTokenAsync()` which does `POST /v1/client/sessions/{id}/tokens`. The JWT has ~60s lifetime; refreshing at 50s keeps it valid continuously.

`ClerkAuthClient` implements `ITokenProvider`. `FinaryDelegatingHandler` calls `GetTokenAsync()` to get the current JWT for each API request.

### Session Persistence

`SessionData` is a record holding `SessionId` (string) and `Cookies` (`IReadOnlyCollection<Cookie>`).

`EncryptedFileSessionStore` serializes to JSON, encrypts with DPAPI (`ProtectedData.Protect` with `CurrentUser` scope), and writes to a file. Location is configurable via `FinaryOptions.SessionStorePath`.

### Credential Input

`ICredentialPrompt` abstracts credential collection. Two implementations exist:

- **`ConsoleCredentialPrompt`** (CLI) — Interactive console prompts with masked password input. Used during cold start only.
- **`McpCredentialPrompt`** (MCP server) — Uses MCP Elicitation to prompt the user through the MCP client (e.g., VS Code, Claude Desktop). Falls back to suggesting a CLI run if the client doesn't support elicitation.

---

## 6. API Layer

### HTTP Pipeline

The Finary API uses `HttpClientFactory` with CurlImpersonate as the transport:

```
FinaryApiClient
   |
   v
HttpClient ("Finary", base: https://api.finary.com)
   |
   |-- FinaryDelegatingHandler    auth headers, rate limit, 401/429 retry
   |
   +-- CurlMessageHandler         bridges CurlClient to HttpMessageHandler
         |
         v
    CurlClient(Chrome136) --> api.finary.com (through Cloudflare)
```

**Why two HTTP stacks?**
- **Clerk calls:** `ClerkAuthClient` uses `CurlClient` directly (needs direct cookie control via `CookieContainer`)
- **Finary API calls:** `HttpClientFactory` with `CurlMessageHandler` as primary handler (gets DI, delegating handler pipeline, base address)

### FinaryDelegatingHandler

Injects on every request:
- `Authorization: Bearer {jwt}` (from `ITokenProvider`)
- `Origin: https://app.finary.com`
- `Referer: https://app.finary.com/`
- `x-client-api-version: 2`
- `x-finary-client-id: webapp`
- `Accept: */*`

Resilience:
- **401 Unauthorized:** Refresh token, retry once
- **429 Too Many Requests:** Uses `Retry-After` header (default 5s), up to 3 retries
- **Rate limiting:** Calls `RateLimiter.WaitAsync()` before every request

### RateLimiter

Token-bucket pattern using `SemaphoreSlim`. Enforces ~5 req/s (200ms minimum interval). API analysis showed the browser at ~2.5 req/s; we run at 5 req/s as a comfortable ceiling.

### FinaryApiClient

Partial class split across 5 files:
- **Core** (`FinaryApiClient.cs`): Constructor, org context management, generic `GetAsync<T>` with pagination, `GetCurrentUser`, `GetAllProfiles`
- **Categories** (`FinaryApiClient.Categories.cs`): `GetCategoryAccountsAsync`, `GetCategoryTimeseriesAsync` — generic over `AssetCategory` enum
- **Portfolio** (`FinaryApiClient.Portfolio.cs`): Portfolio summary, timeseries, dividends, geographical/sector allocation, fees, asset list
- **Reference** (`FinaryApiClient.Reference.cs`): Holdings accounts
- **Transactions** (`FinaryApiClient.Transactions.cs`): Paginated transaction retrieval by category

All methods use organization-scoped URLs: `/organizations/{orgId}/memberships/{membershipId}/...`

JSON deserialization uses `JsonSerializerOptions` with `JsonNamingPolicy.SnakeCaseLower`.

---

## 7. Multi-Profile & Unified Export

### Per-Profile Export

Finary supports multiple profiles (memberships within an organization). `GetAllProfilesAsync()` calls `/users/me/organizations` to enumerate all organizations and their members, then maps each membership to a `FinaryProfile(OrgId, MembershipId, ProfileName)`.

The export loop:
```
for each profile:
    api.SetOrganizationContext(profile.OrgId, profile.MembershipId)
    exporter.ExportAsync("finary-export-{slug}.xlsx", api, displayContext)
```

Per-profile exports use `ExportContext { UseDisplayValues = true }` — values are ownership-adjusted (what the user sees in the Finary UI).

### Unified Export

`UnifiedFinaryApiClient` is a decorator over `IFinaryApiClient` that:
1. Iterates all profiles
2. For each profile, calls the underlying API client with that profile's org context
3. Merges results across profiles (accounts, transactions, dividends, holdings)
4. Applies **ownership scaling** — multiplies raw values by the user's ownership share from `OwnershipRepartition`
5. Caches account data to avoid redundant API calls across sheet writers

The unified export uses `ExportContext { UseDisplayValues = false }` — raw values are used since the unified client handles ownership scaling itself.

### ExportContext

```csharp
public sealed record ExportContext
{
    public bool UseDisplayValues { get; init; } = true;
    public string? DisplayCurrencySymbol { get; init; }

    public decimal ResolveValue(decimal? displayValue, decimal? rawValue)
        => (UseDisplayValues ? displayValue ?? rawValue : rawValue ?? displayValue) ?? 0m;

    public string CurrencyFormat => ExcelStyles.GetCurrencyFormat(DisplayCurrencySymbol);
}
```

Sheet writers call `context.ResolveValue(account.DisplayBalance, account.Balance)` to pick the right value depending on whether this is a per-profile or unified export. The `CurrencyFormat` property generates an Excel number format with the user's display currency symbol (e.g., `"€ "#,##0.00`).

---

## 8. Export Pipeline

### WorkbookExporter

Iterates all registered `ISheetWriter` implementations, calls `WriteAsync` on each, and saves the workbook. Handles errors per-sheet (adds an error sheet on failure) so one broken sheet does not block the others.

### Sheet Writers

| Sheet | Class | Data Source |
|-------|-------|-------------|
| Portfolio Summary | `PortfolioSummarySheet` | `GetPortfolioAsync`, `GetPortfolioTimeseriesAsync` |
| Accounts | `AccountsSheet` | `GetCategoryAccountsAsync` (all categories) |
| Transactions | `TransactionsSheet` | `GetCategoryTransactionsAsync` (filtered by `HasTransactions()`: checkings, savings, investments, credits). Columns include asset category and transaction category. |
| Dividends | `DividendsSheet` | `GetPortfolioDividendsAsync` |
| Holdings | `HoldingsSheet` | `GetHoldingsAccountsAsync`, `GetAssetListAsync` |

All writers implement `ISheetWriter`:
```csharp
public interface ISheetWriter
{
    string SheetName { get; }
    Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct);
}
```

### Excel Formatting

`ExcelStyles` provides shared formatting constants and helpers:
- Header row: bold, blue background (#4472C4), white text, centered
- Currency: `#,##0.00` (prefixed with display currency symbol when available, e.g., `"€ "#,##0.00`)
- Percent: `0.00%`
- Date: `yyyy-MM-dd`
- Freeze top row, auto-fit columns

### Currency Handling

Before exporting each profile, `Program.cs` calls `DetectDisplayCurrencySymbolAsync` which:
1. Reads the user's display currency from `GetCurrentUserAsync()` → `UiConfiguration.DisplayCurrency.Symbol`
2. Falls back to scanning account currencies if the user profile doesn't provide one

The detected symbol (e.g., `€`, `$`, `£`) is passed to `ExportContext.DisplayCurrencySymbol`, which feeds into `ExcelStyles.GetCurrencyFormat()` to generate Excel number formats with the correct currency prefix. All monetary columns across all sheets use this format.

---

## 9. MCP Server

`FinaryExport.Mcp` is an MCP (Model Context Protocol) server that exposes Finary data to AI assistants (Claude Desktop, VS Code Copilot, etc.) via stdio transport.

### Architecture

```
Program.cs (Host.CreateApplicationBuilder)
   |
   +-- AddFinaryCore()                    # Shared auth, API, HTTP pipeline from Core
   +-- AutoInitFinaryApiClient            # Decorator: auto-resolves org context lazily
   +-- McpCredentialPrompt                # Elicitation-based credential collection
   +-- AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()
```

All console logging goes to stderr (stdout is reserved for MCP protocol). Console log threshold is set to `LogLevel.Trace` on stderr.

### Authentication

The MCP server tries warm start first (reuses `~/.finaryexport/session.dat` from a previous CLI run). If no session exists, `McpCredentialPrompt` uses MCP Elicitation to prompt the user for email, password, and TOTP code through the MCP client. If the client doesn't support elicitation, it throws with a message suggesting a CLI run to create the session.

### AutoInitFinaryApiClient

Decorator over `IFinaryApiClient` that lazily calls `GetOrganizationContextAsync()` on the first data request. This means MCP users don't need to manually call `get_profiles` + `set_active_profile` — the server auto-initializes with the owner's default profile. Thread-safe via `SemaphoreSlim` double-checked locking.

### Tool Catalog

15 tools across 7 tool classes, all read-only:

| Class | Tools | Description |
|-------|-------|-------------|
| `UserTools` | `get_user_profile`, `get_profiles`, `set_active_profile` | User identity and profile switching |
| `PortfolioTools` | `get_portfolio_summary`, `get_portfolio_timeseries`, `get_portfolio_fees` | Portfolio valuation and history |
| `AccountTools` | `get_accounts`, `get_all_accounts`, `get_category_timeseries` | Accounts by category |
| `TransactionTools` | `get_transactions`, `get_all_transactions` | Transactions (filtered by `HasTransactions()`) |
| `DividendTools` | `get_dividends` | Dividend income summary |
| `HoldingsTools` | `get_holdings`, `get_asset_list` | Security positions |
| `AllocationTools` | `get_geographical_allocation`, `get_sector_allocation` | Portfolio allocation breakdowns |

Tools use `[McpServerToolType]` and `[McpServerTool]` attributes for discovery via `WithToolsFromAssembly()`. Multi-category tools (e.g., `get_all_accounts`) aggregate with per-category error isolation — one failing category doesn't block the others.

### DI Registration (MCP)

```
AddFinaryCore()                           # Shared: CurlClient, ClerkAuthClient, ITokenProvider,
                                          #   TokenRefreshService, RateLimiter, HttpClient "Finary",
                                          #   IFinaryApiClient → FinaryApiClient
Remove IFinaryApiClient registration      # Replace with decorator
AddSingleton<FinaryApiClient>             # Keep raw client accessible
AddSingleton<IFinaryApiClient, AutoInitFinaryApiClient>  # Auto-init decorator
AddSingleton<ICredentialPrompt, McpCredentialPrompt>     # Elicitation auth
AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()
```

---

## 10. Configuration

### FinaryOptions

Bound from `appsettings.json` section `"Finary"`:

```csharp
public sealed class FinaryOptions
{
    public const string SectionName = "Finary";

    public string OutputPath { get; set; } = "finary-export.xlsx";
    public string? SessionStorePath { get; set; }
}
```

**No credentials in config.** Email, password, and TOTP are entered interactively via `ConsoleCredentialPrompt` (CLI) or `McpCredentialPrompt` (MCP server).

### appsettings.json

```json
{
  "Finary": {
    "OutputPath": "finary-export.xlsx"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "System.Net.Http": "Warning"
    }
  }
}
```

CLI option `--output` overrides the `appsettings.json` value.

---

## 11. Infrastructure

### Dependency Injection

`ServiceCollectionExtensions.AddFinaryCore()` in Core registers shared services. Each consumer (CLI, MCP) registers its own `ICredentialPrompt` implementation and export/MCP services.

**Core services (`AddFinaryCore()`):**
```
CurlClient (singleton, Chrome136)
|-- Auth
|   |-- ISessionStore -> EncryptedFileSessionStore
|   |-- ClerkAuthClient (also provides ITokenProvider)
|   +-- TokenRefreshService (IHostedService)
|-- API
|   |-- RateLimiter
|   |-- FinaryDelegatingHandler (transient)
|   |-- HttpClient "Finary" (CurlMessageHandler + FinaryDelegatingHandler)
|   +-- IFinaryApiClient -> FinaryApiClient
```

**CLI adds:** `ICredentialPrompt → ConsoleCredentialPrompt`, `IWorkbookExporter → WorkbookExporter`, `ISheetWriter → [PortfolioSummary, Accounts, Transactions, Dividends, Holdings]`

**MCP adds:** `ICredentialPrompt → McpCredentialPrompt`, `IFinaryApiClient → AutoInitFinaryApiClient` (decorator), `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`

Note: `ICredentialPrompt` is NOT registered by `AddFinaryCore()` — each host must provide its own implementation.

### CurlMessageHandler

Bridges `CurlClient` (Loxifi.CurlImpersonate) to `HttpMessageHandler` so it can serve as the primary handler in `HttpClientFactory`. This gives Finary API requests Chrome 136's TLS fingerprint while keeping the standard `HttpClient` programming model.

### CompactConsoleFormatter

Custom `ConsoleFormatter` for single-line log output. Registered when configuring the host's logging pipeline.

---

## 12. Data Model Summary

### Key Types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `Account` | `Models.Accounts` | Bank/investment account with balances, ownership, nested positions |
| `OwnershipEntry` | `Models.Accounts` | Share percentage + membership for ownership scaling |
| `SecurityPosition` | `Models.Accounts` | Individual security holding within an account |
| `HoldingsAccount` | `Models.Accounts` | Simplified account from holdings endpoint |
| `PortfolioSummary` | `Models.Portfolio` | Overall portfolio value and allocation data |
| `TimeseriesData` | `Models.Portfolio` | Historical value data points |
| `DividendSummary` | `Models.Portfolio` | Dividend income aggregation (contains `DividendEntry`, `NextYearEntry`, `DividendAssetInfo`) |
| `DividendAssetInfo` | `Models.Portfolio` | Asset metadata nested in dividend entries (id, type, name, logo) |
| `AllocationData` | `Models.Portfolio` | Geographical/sector allocation breakdown |
| `AssetListEntry` | `Models.Portfolio` | Top assets by value |
| `FeeSummary` | `Models.Portfolio` | Fee analysis data |
| `Transaction` | `Models.Transactions` | Buy/sell/income/expense record |
| `TransactionCategory` | `Models.Transactions` | Category with subcategories, color, icon |
| `FinaryProfile` | `Models.User` | OrgId + MembershipId + ProfileName |
| `UserProfile` | `Models.User` | User identity, membership list, subscription, UI config |
| `UiConfiguration` | `Models.User` | Display preferences including display currency |
| `DisplayCurrencyInfo` | `Models.User` | Currency code + symbol for display currency |
| `FinaryResponse<T>` | `Models` | Generic `{ result: T, message, error }` response wrapper |
| `FinaryError` | `Models` | Error code + message from API |
| `AssetCategory` | `Models` | Enum of asset categories + extension methods (`HasTransactions`, `ToDisplayName`, `ToUrlSegment`) |

### API Response Pattern

All Finary API responses wrap data in `{ "result": ..., "message": ..., "error": ... }`. `FinaryResponse<T>` handles deserialization. Pagination uses `?page=N&per_page=N` parameters; `FinaryApiClient` loops until a page returns fewer items than `per_page`.

---

## 13. Design Decisions

Key decisions documented in `.squad/decisions.md`:

| ID | Decision | Rationale |
|----|----------|-----------|
| D-curl | CurlImpersonate over raw HttpClient | Cloudflare blocks standard .NET TLS fingerprints |
| D-multiprofile | Export all profiles automatically | User has multiple memberships; one-by-one is tedious |
| D-unified | Unified workbook aggregating all profiles | Cross-profile portfolio view for analysis |
| D13 | Session persistence via DPAPI | Skip cold auth on subsequent runs; `__client` cookie lasts ~90 days |
| D-pii | Synthetic data only in docs and tests | No real names, IBANs, or addresses in the repo |
| D-ratelimit | 5 req/s token bucket + 429 backoff | Conservative ceiling above observed browser rate |
| D-logging | CompactConsoleFormatter | Single-line log output with short level codes |
| D-noxml | No XML doc comments | Regular `//` comments only — keeps code compact |
| D-mcp | MCP server with Core extraction | Expose Finary data to AI assistants; shared library avoids duplication |
| D-mcp-auth | Elicitation auth for MCP | Warm start from session.dat, cold start via MCP Elicitation prompts |

---

*Living document. Updated as the implementation evolves. — Rusty*
