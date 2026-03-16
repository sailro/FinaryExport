# FinaryExport (and MCP Server)

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

## MCP Server — Talk to Your Portfolio

FinaryExport includes an [MCP](https://modelcontextprotocol.io) server that lets you ask questions about your Finary data in plain language from any AI assistant — GitHub Copilot, Claude Desktop, or any MCP-compatible client. No API knowledge needed. Just ask.

### What You Can Do

Here are some things you can say to your AI assistant once the server is set up:

**Portfolio overview**
> "What's my total portfolio value?"
> "How has my portfolio performed over the last 6 months?"
> "Give me a breakdown of fees across all my accounts"

**Accounts & holdings**
> "Show me all my investment accounts"
> "What accounts does my daughter have?"
> "List my holdings with their current P&L"
> "What are my individual positions worth right now?"

**Transactions**
> "Show me all transactions for the last month"
> "What buys and sells did I make this year in my brokerage account?"

**Dividends**
> "Show me the dividends I'll receive this year"
> "What was my dividend income last year?"

**Allocation**
> "How are my investments allocated by sector?"
> "Show me my geographical allocation"

**Multi-profile (family accounts)**
> "Switch to my daughter's profile and show me her transactions"
> "What profiles are available?"
> "Switch to the kids' account"

The AI assistant figures out which data to fetch — you just describe what you want to know.

### Setup

Add this to your MCP configuration file and restart your client:

- **GitHub Copilot (CLI or VS Code):** `~/.copilot/mcp-config.json` or `.vscode/mcp.json`
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

> **Tip:** You can replace `dotnet run --project ...` with the path to a published executable for faster startup.

### Authentication

The first time you use a Finary tool, the assistant will prompt you for your **email**, **password**, and **TOTP code** (if you have 2FA enabled). Your credentials are used once to log in and are never saved. The session token is encrypted with DPAPI and stored locally at `~/.finaryexport/session.dat`, so you won't need to log in again until the session expires.

If you've already used the CLI exporter, the MCP server picks up your existing session automatically — no extra login required.

> **Note:** If your MCP client doesn't support interactive prompts (elicitation), run the CLI exporter once first (`dotnet run --project src/FinaryExport -- export`) to create the session file.

### Multi-Profile

If you have a family account with multiple profiles (e.g., personal + kids), just ask the assistant to switch:

> "Switch to my daughter's profile"

The assistant discovers all your available profiles automatically. All subsequent questions use the selected profile until you switch again.

### Tool Reference

<details>
<summary>Available tools (for developers and advanced users)</summary>

The server exposes 16 read-only tools. Most accept an optional `period` parameter (`all`, `1d`, `1w`, `1m`, `3m`, `6m`, `1y`, `5y`).

| Tool | What it does |
|------|-------------|
| `get_user_profile` | Authenticated user's profile info (name, email, currency) |
| `get_profiles` | Lists all available profiles (personal + org memberships) |
| `set_active_profile` | Switches the active profile for subsequent queries |
| `get_portfolio_summary` | Total portfolio valuation, gross/net, period performance |
| `get_portfolio_timeseries` | Historical portfolio value over time |
| `get_portfolio_fees` | Fee analysis: annual, cumulated, potential savings |
| `get_accounts` | Accounts for a specific asset category |
| `get_all_accounts` | Accounts across all asset categories |
| `get_category_timeseries` | Historical value for a specific category |
| `get_transactions` | Transactions for a category (checkings, savings, investments, credits) |
| `get_all_transactions` | Transactions across all supported categories |
| `get_holdings` | Investment holdings with security positions and balances |
| `get_asset_list` | Flat list of all positions with current value and P&L |
| `get_dividends` | Dividend summary: annual income, yield, past and upcoming |
| `get_geographical_allocation` | Portfolio allocation by region |
| `get_sector_allocation` | Portfolio allocation by economic sector |

</details>

## License

[MIT](LICENSE)
