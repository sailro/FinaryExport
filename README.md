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

### Force re-authentication

```bash
dotnet run --project src/FinaryExport -- clear-session
```

Discards the cached session and forces a fresh login on the next export.

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

### Session Storage & Security

**Your email, password, and TOTP codes are never saved to disk.** They are used only during the interactive login prompt and discarded immediately after authentication.

What IS saved is a session token and cookies, stored in:

```
~/.finaryexport/session.dat
```

This file is encrypted at rest using [**DPAPI** (Data Protection API)](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection) — a well-established Windows cryptographic framework used by Chrome, Edge, and other major applications to protect sensitive data. DPAPI encryption is scoped to the current Windows user account, meaning the file cannot be decrypted by other users on the same machine or if copied to another computer.

To discard the cached session, use `clear-session` (see above) or simply delete `~/.finaryexport/session.dat`.

## Currency Handling

Finary lets you choose a **display currency** in your account settings (e.g. EUR, USD). All monetary values in the export (balances, prices, P&L, dividends…) are expressed in this display currency, and the corresponding symbol (€, $, £…) is automatically applied to every amount column in the workbook.

Each account and transaction also has a **Native Currency** column showing the original currency of the underlying asset or operation — for example, a US brokerage account will show `USD` as native currency even if your display currency is set to EUR.

> **Tip:** To change the currency used in the export, update your display currency in Finary's settings and re-export.

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

Each account sheet has columns: **Name**, **Institution**, **Balance**, **Native Currency**, **Buying Value**, **Unrealized P&L**, **Annual Yield**, **IBAN**, **Opened At**, **Last Sync**.

### Holdings

Individual security positions from investment accounts: **Account**, **Name**, **ISIN**, **Symbol**, **Type**, **Quantity**, **Buy Price**, **Current Price**, **Value**, **+/- Value**, **+/- %**.

### Transactions

Buy/sell/income/expense records across checking, savings, investment, and credit accounts: **Category**, **Date**, **Name**, **Value**, **Type**, **Account**, **Institution**, **Native Currency**, **Commission**, **Transaction Category**.

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

## MCP Server

FinaryExport includes an [MCP (Model Context Protocol)](https://modelcontextprotocol.io) server that exposes your Finary data as tools for AI assistants. This lets you query your portfolio, accounts, transactions, dividends, and more directly from Copilot, Claude Desktop, or any MCP-compatible client.

### Configuration

Add the following entry to your MCP configuration file:

- **GitHub Copilot CLI / VS Code:** `~/.copilot/mcp-config.json` (or `.vscode/mcp.json` in your workspace)
- **Claude Desktop:** `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "finary": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "src/FinaryExport.Mcp"]
    }
  }
}
```

> **Tip:** Replace the `args` with the path to a published executable if you prefer not to use `dotnet run`.

### Authentication

The MCP server authenticates using **MCP Elicitation** — a protocol feature that lets the server prompt the AI client for user input. On first use (when no cached session exists), the server asks for your Finary email, password, and TOTP code via an elicitation form. After successful authentication, the session is encrypted with DPAPI and persisted to:

```
~/.finaryexport/session.dat
```

This is the same session file used by the CLI exporter. If you've already run the CLI and authenticated, the MCP server reuses that session automatically — no re-authentication needed.

If your MCP client doesn't support elicitation, run the CLI exporter first (`dotnet run --project src/FinaryExport -- export`) to create the session file, then start the MCP server.

### Multi-Profile Support

If you have multiple Finary profiles (personal + organization memberships), the server auto-initializes with your default profile. To switch profiles:

1. Call **`get_profiles`** to list all available profiles with their `org_id` and `membership_id`
2. Call **`set_active_profile`** with the desired `org_id` and `membership_id`
3. All subsequent tool calls use the selected profile

### Available Tools

The MCP server exposes 16 read-only tools:

| Tool | Description |
|------|-------------|
| `get_user_profile` | Get the authenticated user's profile (name, email, subscription, display currency) |
| `get_profiles` | List all available profiles (personal + organization memberships) |
| `set_active_profile` | Switch the active profile for subsequent queries |
| `get_portfolio_summary` | Get total portfolio valuation with gross/net values and period performance |
| `get_portfolio_timeseries` | Get historical portfolio value over time (for charting/trends) |
| `get_portfolio_fees` | Get fee analysis: annual fees, cumulated fees, potential savings |
| `get_accounts` | Get all accounts for a specific asset category |
| `get_all_accounts` | Get accounts across all asset categories in a single call |
| `get_category_timeseries` | Get historical value timeseries for a specific asset category |
| `get_transactions` | Get transactions for a category (checkings, savings, investments, credits only) |
| `get_all_transactions` | Get transactions across all transaction-capable categories |
| `get_holdings` | Get all holdings accounts with security positions and balances |
| `get_asset_list` | Get flat list of all individual positions with current value and P&L |
| `get_dividends` | Get dividend income summary: annual income, yield, past and upcoming dividends |
| `get_geographical_allocation` | Get portfolio allocation by geographical region |
| `get_sector_allocation` | Get portfolio allocation by economic sector |

Most tools accept an optional `period` parameter: `all`, `1d`, `1w`, `1m`, `3m`, `6m`, `1y`, `5y`.

## License

[MIT](LICENSE)
