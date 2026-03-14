# FinaryExport

A .NET command-line tool that exports your [Finary](https://finary.com) wealth management data to Excel (`.xlsx`) files.

Connects to the Finary API, authenticates via Clerk, and exports portfolio summaries, account balances, holdings, transactions, and dividends — one workbook per profile, plus a unified workbook across all profiles.

## Prerequisites

- **Windows 10 or later** — required (DPAPI session encryption and TLS impersonation depend on Windows-only native libraries)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- NuGet packages are restored automatically by `dotnet build`

## Build

```bash
dotnet build
```

## Usage

All commands are run from the repository root. The `--project` flag tells `dotnet run` which project to execute.

### Export data

```bash
dotnet run --project src/FinaryExport -- export
```

Exports all profiles to the default output directory. Each profile gets its own file (e.g., `finary-export-jean-dupont.xlsx`) and a unified workbook (`finary-export-unified.xlsx`) is generated combining all profiles.

### Export with custom output path

```bash
dotnet run --project src/FinaryExport -- export --output myfile.xlsx
```

### Export with time period filter

```bash
dotnet run --project src/FinaryExport -- export --period 1y
```

Supported periods: `1d`, `1w`, `1m`, `3m`, `6m`, `1y`, `all` (default: `all`).

### Force re-authentication

```bash
dotnet run --project src/FinaryExport -- export --clear-session
```

Discards the cached session and forces a fresh login for this run. Can also be used as a standalone command:

```bash
dotnet run --project src/FinaryExport -- clear-session
```

### Show version

```bash
dotnet run --project src/FinaryExport -- version
```

## Authentication

On first run, FinaryExport prompts interactively for:

1. **Email** — your Finary account email
2. **Password** — your Finary account password
3. **TOTP code** — a 6-digit code from your authenticator app (if 2FA is enabled)

After successful login, the session is encrypted and cached locally using DPAPI (Windows). Subsequent runs reuse the cached session until it expires, with automatic token refresh in the background.

## Output

Each profile export generates an `.xlsx` workbook. A unified workbook combining all profiles is also generated. A full profile for instance produces **14 sheets**:

### Portfolio Summary

Overall portfolio metrics:
- Gross/net total, evolution (€ and %)
- Per-category breakdown: account count and total balance for each asset category

### Account Sheets (one per category with data)

Up to 10 category-specific sheets — only created if the category has accounts:

| Sheet | Category |
|-------|----------|
| Checkings | Bank checking accounts |
| Savings | Savings accounts |
| Investments | Investment/brokerage accounts |
| Real Estate | Property holdings (SCPIs, etc.) |
| Cryptos | Cryptocurrency accounts |
| Fonds Euro | Euro-denominated funds |
| Commodities | Commodity holdings |
| Credits | Loans and credit lines |
| Other Assets | Miscellaneous assets |
| Startups | Startup equity/investments |

Each account sheet has columns: **Name**, **Institution**, **Balance**, **Currency**, **Buying Value**, **Unrealized P&L**, **Annual Yield**, **IBAN**, **Opened At**, **Last Sync**.

### Holdings

Individual security positions from investment accounts: **Account**, **Name**, **ISIN**, **Symbol**, **Type**, **Quantity**, **Buy Price**, **Current Price**, **Value**, **+/- Value**, **+/- %**.

### Transactions

Buy/sell/income/expense records across checking, savings, investment, and credit accounts: **Category**, **Date**, **Name**, **Value**, **Type**, **Account**, **Institution**, **Currency**, **Commission**.

> Note: Only 4 categories support transactions in the Finary API (checkings, savings, investments, credits). Real estate, cryptos, and others do not have transaction endpoints.

### Dividends

Three sections in one sheet:
- **Summary** — annual income, past income, projected next year, yield %
- **Past Dividends** — investment name, amount, payment date, type, category
- **Upcoming Dividends** — investment name, projected amount, date, status, category

### Multi-Profile

The tool discovers all Finary profiles (personal + organization memberships) and exports:
- One workbook per profile: `finary-export-{name}.xlsx` (ownership-adjusted values)
- One unified workbook: `finary-export-unified.xlsx` (aggregated raw values across all profiles)

## License

[MIT](LICENSE)
