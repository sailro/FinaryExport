# FinaryExport

A .NET command-line tool that exports your [Finary](https://finary.com) wealth management data to Excel (`.xlsx`) files.

Connects to the Finary API, authenticates via Clerk, and exports portfolio summaries, account balances, holdings, transactions, and dividends — one workbook per profile, plus a unified workbook across all profiles.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- NuGet packages are restored automatically by `dotnet build`

## Build

```bash
dotnet build
```

## Usage

### Export data

```bash
dotnet run -- export
```

Exports all profiles to the default output directory. Each profile gets its own file (e.g., `finary-export-jean-dupont.xlsx`) and a unified workbook (`finary-export-unified.xlsx`) is generated combining all profiles.

### Export with custom output path

```bash
dotnet run -- export --output myfile.xlsx
```

### Export with time period filter

```bash
dotnet run -- export --period 1y
```

Supported periods: `1w`, `1m`, `ytd`, `1y`, `all` (default: `all`).

### Clear cached session

```bash
dotnet run -- clear-session
```

Clears the locally cached authentication session, forcing a fresh login on the next run.

### Show version

```bash
dotnet run -- version
```

## Authentication

On first run, FinaryExport prompts interactively for:

1. **Email** — your Finary account email
2. **Password** — your Finary account password
3. **TOTP code** — a 6-digit code from your authenticator app (if 2FA is enabled)

After successful login, the session is encrypted and cached locally using DPAPI (Windows). Subsequent runs reuse the cached session until it expires, with automatic token refresh in the background.

## Output

The tool generates one `.xlsx` workbook per Finary profile, plus a unified workbook that merges data from all profiles. Each workbook contains sheets for:

- **Portfolio Summary** — overall portfolio value, allocation, performance
- **Accounts** — all bank and investment accounts across asset categories
- **Holdings** — individual security positions
- **Transactions** — buy/sell/income/expense records
- **Dividends** — dividend income summary

## License

Private — not licensed for redistribution.
