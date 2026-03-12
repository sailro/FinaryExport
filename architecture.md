# FinaryExport — Architecture Document

> **Author:** Rusty (Lead)
> **Revised:** 2026-03-14 — updated to reflect current implementation
> **Status:** Living document tracking current implementation state

---

## Table of Contents

1. [Overview](#1-overview)
2. [Project Structure](#2-project-structure)
3. [Dependencies](#3-dependencies)
4. [CLI Layer](#4-cli-layer)
5. [Authentication](#5-authentication)
6. [API Layer](#6-api-layer)
7. [Multi-Profile & Unified Export](#7-multi-profile--unified-export)
8. [Export Pipeline](#8-export-pipeline)
9. [Configuration](#9-configuration)
10. [Infrastructure](#10-infrastructure)
11. [Data Model Summary](#11-data-model-summary)
12. [Design Decisions](#12-design-decisions)

---

## 1. Overview

FinaryExport is a .NET 10 console application that exports Finary wealth management data to Excel workbooks. It authenticates via Clerk using TLS-fingerprinted HTTP requests (CurlImpersonate), iterates over all user profiles (memberships), and produces one `.xlsx` per profile plus a unified workbook combining all profiles.

Key technical choices:
- **CurlImpersonate** — Chrome 136 TLS fingerprint to bypass Cloudflare bot detection
- **Interactive credentials** — email/password/TOTP prompted at runtime; no secrets in config
- **DPAPI session persistence** — encrypted cookie/session cache for warm-start auth
- **System.CommandLine** — structured CLI with `export`, `clear-session`, `version` commands
- **ClosedXML** — Excel generation without COM interop

---

## 2. Project Structure

```
src/FinaryExport/
+-- Program.cs                          # CLI entry point (System.CommandLine)
+-- FinaryExport.csproj
+-- appsettings.json
|
+-- Api/
|   +-- IFinaryApiClient.cs             # API contract
|   +-- FinaryApiClient.cs              # Partial class: core setup, org context, pagination
|   +-- FinaryApiClient.Categories.cs   # Category-generic endpoints
|   +-- FinaryApiClient.Portfolio.cs    # Portfolio, timeseries, dividends, allocations, fees
|   +-- FinaryApiClient.Reference.cs    # Asset list, holdings accounts
|   +-- FinaryApiClient.Transactions.cs # Transaction endpoints (paginated)
|   +-- UnifiedFinaryApiClient.cs       # Decorator: merges data across all profiles
|   +-- RateLimiter.cs                  # Token-bucket, ~5 req/s
|
+-- Auth/
|   +-- ClerkAuthClient.cs              # Clerk auth (CurlImpersonate), cold/warm start
|   +-- ITokenProvider.cs               # JWT provider contract
|   +-- ICredentialPrompt.cs            # Credential input contract
|   +-- ConsoleCredentialPrompt.cs      # Interactive console prompts
|   +-- ISessionStore.cs                # Session persistence contract
|   +-- EncryptedFileSessionStore.cs    # DPAPI-encrypted file storage
|   +-- SessionData.cs                  # Record: SessionId + Cookies
|   +-- TokenRefreshService.cs          # IHostedService: refreshes JWT every 50s
|
+-- Configuration/
|   +-- FinaryOptions.cs                # OutputPath, Period, Locale, SessionStorePath, ClearSession
|
+-- Export/
|   +-- ExportContext.cs                # Controls display vs raw value selection
|   +-- IWorkbookExporter.cs            # Exporter contract
|   +-- WorkbookExporter.cs             # Iterates ISheetWriter list, saves .xlsx
|   +-- Formatting/
|   |   +-- ExcelStyles.cs              # Currency/percent/date formats, header styling
|   +-- Sheets/
|       +-- ISheetWriter.cs             # Sheet writer contract
|       +-- PortfolioSummarySheet.cs    # Portfolio value, allocation, performance
|       +-- AccountsSheet.cs            # Accounts across all asset categories
|       +-- TransactionsSheet.cs        # Buy/sell/income/expense records
|       +-- DividendsSheet.cs           # Dividend income
|       +-- HoldingsSheet.cs            # Individual security positions
|
+-- Infrastructure/
|   +-- ServiceCollectionExtensions.cs  # DI registration
|   +-- CurlMessageHandler.cs           # Bridges CurlClient to HttpMessageHandler
|   +-- FinaryDelegatingHandler.cs      # Auth headers, rate limit, 401/429 retry
|   +-- CompactConsoleFormatter.cs      # Single-line log formatter
|
+-- Models/
    +-- ApiEnvelope.cs                  # Generic { result: T } wrapper
    +-- AssetCategory.cs                # Enum: checking, savings, investments, etc.
    +-- Accounts/
    |   +-- Account.cs                  # Account with balances, ownership, nested positions
    |   +-- HoldingsAccount.cs
    |   +-- OwnershipEntry.cs           # Share + membership for ownership scaling
    |   +-- SecurityInfo.cs
    |   +-- SecurityPosition.cs
    +-- Auth/                           # Clerk auth response models
    +-- Portfolio/                       # PortfolioSummary, TimeseriesData, DividendSummary
    +-- Transactions/                   # Transaction model
    +-- User/
        +-- FinaryProfile.cs            # OrgId + MembershipId + ProfileName
        +-- Membership.cs
        +-- Organization.cs
        +-- UserProfile.cs
```

---

## 3. Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| ClosedXML | 0.104.2 | Excel workbook generation |
| Loxifi.CurlImpersonate | 1.1.0 | Chrome 136 TLS fingerprint (Cloudflare bypass) |
| Microsoft.Extensions.Hosting | 9.0.4 | Generic host, DI, logging, hosted services |
| Microsoft.Extensions.Http | 9.0.4 | HttpClientFactory for Finary API |
| System.CommandLine | 2.0.0-beta4 | CLI argument parsing |
| System.Security.Cryptography.ProtectedData | 9.0.4 | DPAPI encryption for session store |

**Target framework:** `net10.0`

---

## 4. CLI Layer

`Program.cs` uses `System.CommandLine` to define three commands:

### `export` (default)

Options:
- `--output` / `-o` — Output file path (default: `finary-export.xlsx`)
- `--period` / `-p` — Time period: `1w`, `1m`, `ytd`, `1y`, `all` (default: `all`)
- `--locale` — Locale for formatting (default: `fr-FR`)

Flow:
1. Build `IHost` with `ConfigureHost` (binds `FinaryOptions`, registers all services)
2. Resolve `ClerkAuthClient`, call `LoginAsync()` (warm or cold start)
3. Call `GetAllProfilesAsync()` to enumerate memberships
4. Loop over each profile:
   - `SetOrganizationContext(orgId, membershipId)`
   - `ExportAsync(profilePath, api, new ExportContext { UseDisplayValues = true })`
5. Create `UnifiedFinaryApiClient` wrapping `IFinaryApiClient`
6. `ExportAsync(unifiedPath, unifiedApi, new ExportContext { UseDisplayValues = false })`
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

`ICredentialPrompt` abstracts credential collection. `ConsoleCredentialPrompt` implements it with interactive console prompts (password input is masked). No credentials are stored in configuration files or environment variables — they are entered at runtime during cold start only.

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

Resilience:
- **401 Unauthorized:** Refresh token, retry once
- **429 Too Many Requests:** Uses `Retry-After` header (default 5s), up to 3 retries
- **Rate limiting:** Calls `RateLimiter.WaitAsync()` before every request

### RateLimiter

Token-bucket pattern using `SemaphoreSlim`. Enforces ~5 req/s (200ms minimum interval). API analysis showed the browser at ~2.5 req/s; we run at 5 req/s as a comfortable ceiling.

### FinaryApiClient

Partial class split across 5 files:
- **Core** (`FinaryApiClient.cs`): Constructor, org context management, generic `GetAsync<T>` with pagination, `GetCurrentUser`
- **Categories** (`FinaryApiClient.Categories.cs`): `GetCategoryAccountsAsync`, `GetCategoryTimeseriesAsync`, `GetCategoryTransactionsAsync` — generic over `AssetCategory` enum
- **Portfolio** (`FinaryApiClient.Portfolio.cs`): Portfolio summary, timeseries, dividends, geographical/sector allocation, fees
- **Reference** (`FinaryApiClient.Reference.cs`): Asset list, holdings accounts
- **Transactions** (`FinaryApiClient.Transactions.cs`): Paginated transaction retrieval

All methods use organization-scoped URLs: `/organizations/{orgId}/memberships/{membershipId}/...`

JSON deserialization uses `JsonSerializerOptions` with `JsonNamingPolicy.SnakeCaseLower`.

---

## 7. Multi-Profile & Unified Export

### Per-Profile Export

Finary supports multiple profiles (memberships within an organization). `GetAllProfilesAsync()` calls `GetCurrentUser()` then maps each membership to a `FinaryProfile(OrgId, MembershipId, ProfileName)`.

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

    public decimal ResolveValue(decimal? displayValue, decimal? rawValue)
        => (UseDisplayValues ? displayValue ?? rawValue : rawValue ?? displayValue) ?? 0m;
}
```

Sheet writers call `context.ResolveValue(account.DisplayBalance, account.Balance)` to pick the right value depending on whether this is a per-profile or unified export.

---

## 8. Export Pipeline

### WorkbookExporter

Iterates all registered `ISheetWriter` implementations, calls `WriteAsync` on each, and saves the workbook. Handles errors per-sheet (adds an error sheet on failure) so one broken sheet does not block the others.

### Sheet Writers

| Sheet | Class | Data Source |
|-------|-------|-------------|
| Portfolio Summary | `PortfolioSummarySheet` | `GetPortfolioAsync`, `GetPortfolioTimeseriesAsync` |
| Accounts | `AccountsSheet` | `GetCategoryAccountsAsync` (all categories) |
| Transactions | `TransactionsSheet` | `GetCategoryTransactionsAsync` (all categories) |
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
- Currency: `#,##0.00`
- Percent: `0.00%`
- Date: `yyyy-MM-dd`
- Freeze top row, auto-fit columns

---

## 9. Configuration

### FinaryOptions

Bound from `appsettings.json` section `"Finary"`:

```csharp
public sealed class FinaryOptions
{
    public string OutputPath { get; set; } = "finary-export.xlsx";
    public string Period { get; set; } = "all";
    public string Locale { get; set; } = "fr-FR";
    public string? SessionStorePath { get; set; }
    public bool ClearSession { get; set; }
}
```

**No credentials in config.** Email, password, and TOTP are entered interactively via `ConsoleCredentialPrompt`.

### appsettings.json

```json
{
  "Finary": {
    "OutputPath": "finary-export.xlsx",
    "Period": "all",
    "Locale": "fr-FR"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "System.Net.Http.HttpClient": "Warning"
    }
  }
}
```

CLI options (`--output`, `--period`, `--locale`) override `appsettings.json` values.

---

## 10. Infrastructure

### Dependency Injection

`ServiceCollectionExtensions.AddFinaryExport()` registers all services:

```
CurlClient (singleton, Chrome136)
|-- Auth
|   |-- ICredentialPrompt -> ConsoleCredentialPrompt
|   |-- ISessionStore -> EncryptedFileSessionStore
|   |-- ClerkAuthClient (also provides ITokenProvider)
|   +-- TokenRefreshService (IHostedService)
|-- API
|   |-- RateLimiter
|   |-- FinaryDelegatingHandler (transient)
|   |-- HttpClient "Finary" (CurlMessageHandler + FinaryDelegatingHandler)
|   +-- IFinaryApiClient -> FinaryApiClient
+-- Export
    |-- IWorkbookExporter -> WorkbookExporter
    +-- ISheetWriter -> [PortfolioSummary, Accounts, Transactions, Dividends, Holdings]
```

### CurlMessageHandler

Bridges `CurlClient` (Loxifi.CurlImpersonate) to `HttpMessageHandler` so it can serve as the primary handler in `HttpClientFactory`. This gives Finary API requests Chrome 136's TLS fingerprint while keeping the standard `HttpClient` programming model.

### CompactConsoleFormatter

Custom `ConsoleFormatter` for single-line log output. Registered when configuring the host's logging pipeline.

---

## 11. Data Model Summary

### Key Types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `Account` | `Models.Accounts` | Bank/investment account with balances, ownership, nested positions |
| `OwnershipEntry` | `Models.Accounts` | Share percentage + membership for ownership scaling |
| `SecurityPosition` | `Models.Accounts` | Individual security holding within an account |
| `HoldingsAccount` | `Models.Accounts` | Simplified account from holdings endpoint |
| `PortfolioSummary` | `Models.Portfolio` | Overall portfolio value and allocation data |
| `TimeseriesData` | `Models.Portfolio` | Historical value data points |
| `DividendSummary` | `Models.Portfolio` | Dividend income aggregation |
| `Transaction` | `Models.Transactions` | Buy/sell/income/expense record |
| `FinaryProfile` | `Models.User` | OrgId + MembershipId + ProfileName |
| `UserProfile` | `Models.User` | User identity and membership list |
| `ApiEnvelope<T>` | `Models` | Generic `{ result: T }` response wrapper |
| `AssetCategory` | `Models` | Enum of asset categories (checking, savings, etc.) |

### API Response Pattern

All Finary API responses wrap data in `{ "result": ... }`. `ApiEnvelope<T>` handles deserialization. Pagination uses `?page=N` parameters; `FinaryApiClient` loops until an empty page is returned.

---

## 12. Design Decisions

Key decisions documented in `.squad/decisions.md`:

| ID | Decision | Rationale |
|----|----------|-----------|
| D-curl | CurlImpersonate over raw HttpClient | Cloudflare blocks standard .NET TLS fingerprints |
| D-multiprofile | Export all profiles automatically | User has multiple memberships; one-by-one is tedious |
| D-unified | Unified workbook aggregating all profiles | Cross-profile portfolio view for analysis |
| D-pii | Synthetic data only in docs and tests | No real names, IBANs, or addresses in the repo |
| D-ratelimit | 5 req/s token bucket + 429 backoff | Conservative ceiling above observed browser rate |
| D-noxml | No XML doc comments | Regular `//` comments only — keeps code compact |

---

*Living document. Updated as the implementation evolves. — Rusty*
