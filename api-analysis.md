# Finary API Analysis — Traffic Capture Report

> **Analyst:** Livingston (Protocol Analyst)
> **Source:** `API traffic data` — 62MB, 2104 entries
> **Captured:** 2026-03-12 ~07:50–08:00 UTC
> **Domains analyzed:** `clerk.finary.com` (18 entries), `api.finary.com` (1192 entries)

---

## Table of Contents

1. [Authentication Flow](#1-authentication-flow)
2. [JWT Token Analysis](#2-jwt-token-analysis)
3. [Token Refresh Mechanism](#3-token-refresh-mechanism)
4. [Autonomous Auth Implementation Guide](#4-autonomous-auth-implementation-guide)
5. [API Overview](#5-api-overview)
6. [API Endpoints — User & Session](#6-api-endpoints--user--session)
7. [API Endpoints — Portfolio](#7-api-endpoints--portfolio)
8. [API Endpoints — Asset Categories](#8-api-endpoints--asset-categories)
9. [API Endpoints — Transactions & Cashflow](#9-api-endpoints--transactions--cashflow)
10. [API Endpoints — Other](#10-api-endpoints--other)
11. [Common Patterns](#11-common-patterns)
12. [Implementation Notes](#12-implementation-notes)

---

## 1. Authentication Flow

Finary uses **Clerk** (clerk.finary.com) as its identity provider. The auth flow is a multi-step process observed in the traffic:

### Step 1: Get Environment Configuration

```
GET https://clerk.finary.com/v1/environment?__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4
Origin: https://app.finary.com
```

Returns Clerk instance configuration including:
- `auth_config.identification_strategies`: `["email_address", "oauth_apple", "oauth_google"]`
- `auth_config.second_factors`: `["backup_code", "totp"]`
- `auth_config.single_session_mode`: `true`
- `display_config.instance_environment_type`: `"production"`

### Step 2: Get/Create Client

```
GET https://clerk.finary.com/v1/client?__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4
Origin: https://app.finary.com
```

Returns a client object and sets essential cookies:
- **`__client`** — Clerk client token (primary session identifier)
- **`__client_uat`** — Client updated-at timestamp
- **`__client_uat_{instance}`** — Instance-specific UAT variant

These cookies are **required** for all subsequent Clerk API calls.

### Step 3: Sign In (Email + Password)

```
POST https://clerk.finary.com/v1/client/sign_ins?__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4
Content-Type: application/x-www-form-urlencoded
Cookie: __client=[REDACTED]; __client_uat=[REDACTED]; __client_uat_{instance}=[REDACTED]
Origin: https://app.finary.com

locale=fr-FR&identifier=[REDACTED_EMAIL]&password=[REDACTED_PASSWORD]
```

- **Method:** POST with form-encoded body
- **Fields:** `locale`, `identifier` (email), `password`
- **Response:** Returns a `sign_in` object with an ID (format: `sia_XXXXX...`) and status indicating first factor is verified but second factor is needed
- **Response sets updated cookies:** `__client_uat`, `__client_uat_{instance}`

### Step 4: Second Factor (TOTP)

```
POST https://clerk.finary.com/v1/client/sign_ins/{sign_in_id}/attempt_second_factor?__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4
Content-Type: application/x-www-form-urlencoded
Cookie: __client=[REDACTED]; __client_uat=[REDACTED]; __client_uat_{instance}=[REDACTED]
Origin: https://app.finary.com

strategy=totp&code=123456
```

- **`{sign_in_id}`** is from Step 3 response (e.g., `sia_EXAMPLE789abcdefghijklmnop`)
- **Fields:** `strategy=totp`, `code` (6-digit TOTP code)
- **On success:** Returns completed sign-in with `created_session_id`
- **Response sets cookies:** `__client` (updated), `__client_uat`, `__client_uat_{instance}`

> **⚠️ 2FA Consideration:** This account uses TOTP. For autonomous auth, the tool will need either:
> - A TOTP secret to generate codes programmatically
> - A backup code (one-time use)
> - Or the user disables 2FA for the automation account

### Step 5: Session Touch (Activate Session)

```
POST https://clerk.finary.com/v1/client/sessions/{session_id}/touch?__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4
Content-Type: application/x-www-form-urlencoded
Cookie: __client=[REDACTED]; __client_uat=[REDACTED]; __client_uat_{instance}=[REDACTED]
Origin: https://app.finary.com

active_organization_id=
```

- **`{session_id}`** is from Step 4 response (e.g., `sess_EXAMPLE456abcdefghijklmnop`)
- Makes the session active and may return a `last_active_token` containing the first JWT

### Step 6: Get Session Token (JWT)

```
POST https://clerk.finary.com/v1/client/sessions/{session_id}/tokens?__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4
Content-Type: application/x-www-form-urlencoded
Cookie: __client=[REDACTED]; __client_uat=[REDACTED]; __client_uat_{instance}=[REDACTED]
Origin: https://app.finary.com

organization_id=
```

**Response:**
```json
{
  "object": "token",
  "jwt": "[REDACTED_JWT]"
}
```

This JWT is then used as `Authorization: Bearer [JWT]` for all `api.finary.com` calls.

---

## 2. JWT Token Analysis

Decoded JWT structure (RS256 signed):

### Header
| Field | Value | Description |
|-------|-------|-------------|
| `alg` | `RS256` | RSA SHA-256 signature |
| `typ` | `JWT` | Token type |
| `kid` | `ins_EXAMPLE123abcdefghijklmnop` | Key ID (Clerk instance key) |
| `cat` | `cl_EXAMPLE_CATEGORY` | Clerk category identifier |

### Payload Claims
| Claim | Type | Description |
|-------|------|-------------|
| `azp` | string | Authorized party: `https://app.finary.com` |
| `exp` | number | Expiration timestamp |
| `fva` | array | Feature version array: `[0, 0]` |
| `iat` | number | Issued-at timestamp |
| `iss` | string | Issuer: `https://clerk.finary.com` |
| `nbf` | number | Not-before timestamp |
| `sid` | string | Session ID (32 chars) |
| `sts` | string | Session status: `"active"` |
| `sub` | string | Subject / User ID (32 chars, format: `user_XXXX...`) |

### Token Characteristics
- **Total length:** ~766 characters
- **Signature:** RS256 (342 chars base64url)
- **Lifetime:** **60 seconds** (exp - iat = 60s)
- **Clock skew:** 10 seconds (nbf = iat - 10s)

---

## 3. Token Refresh Mechanism

### Observed Refresh Pattern

11 token refresh requests observed over ~8 minutes of browsing:

| # | Time (UTC) | API Entry | Interval |
|---|-----------|-----------|----------|
| 1 | 07:50:48 | 277 | — |
| 2 | 07:51:34 | 806 | 46s |
| 3 | 07:52:20 | 860 | 46s |
| 4 | 07:53:06 | 1083 | 46s |
| 5 | 07:53:53 | 1279 | 47s |
| 6 | 07:54:39 | 1450 | 46s |
| 7 | 07:55:25 | 1597 | 46s |
| 8 | 07:56:12 | 1696 | 47s |
| 9 | 07:56:59 | 1786 | 47s |
| 10 | 07:57:45 | 1863 | 46s |
| 11 | 07:58:34 | 1970 | 49s |

### Key Findings

- **Token TTL:** 60 seconds
- **Refresh interval:** ~46-49 seconds (proactive, before expiry)
- **Refresh endpoint:** `POST /v1/client/sessions/{session_id}/tokens`
- **Refresh uses:** The `__client` cookie (NOT the old JWT)
- **Body:** `organization_id=` (empty, form-encoded)
- **Response:** `{ "object": "token", "jwt": "[new JWT]" }`

### Implementation Strategy

The client should:
1. After login, call `/tokens` to get initial JWT
2. Track `iat` claim from JWT payload
3. Refresh ~10 seconds before expiry (i.e., every ~50 seconds)
4. Use new JWT for subsequent API calls immediately
5. Keep the `__client` cookie alive — it's the long-lived session credential

---

## 4. Autonomous Auth Implementation Guide

### What the .NET client needs to replicate:

#### Prerequisites
- Email address
- Password
- TOTP secret (for 2FA code generation) OR backup codes

#### Cookie Jar
Must persist cookies across requests. Key cookies:
- `__client` — Long-lived Clerk session (set by `/v1/client` and updated by sign-in)
- `__client_uat` — Updated-at timestamp
- `__client_uat_{instance}` — Instance-specific variant

#### Required Query Parameters (all Clerk calls)
```
__clerk_api_version=2025-11-10
_clerk_js_version=5.125.4
```

#### Required Headers (all Clerk calls)
```
Origin: https://app.finary.com
Referer: https://app.finary.com/
```

#### Login Sequence (Pseudocode)
```
1. GET  /v1/environment           → validate config
2. GET  /v1/client                → establish client, get cookies
3. POST /v1/client/sign_ins       → { identifier, password, locale }
   → extract sign_in_id from response
4. POST /v1/client/sign_ins/{sign_in_id}/attempt_second_factor
   → { strategy: "totp", code: generated_totp }
   → extract session_id from response
5. POST /v1/client/sessions/{session_id}/touch
   → { active_organization_id: "" }
6. POST /v1/client/sessions/{session_id}/tokens
   → { organization_id: "" }
   → extract JWT from response
```

#### Token Refresh Loop
```
while (session active):
    wait ~50 seconds
    POST /v1/client/sessions/{session_id}/tokens
    → { organization_id: "" }
    → update JWT
```

#### Session Lifetime
From Clerk client response:
- `expire_at`: ~90 days from creation
- `abandon_at`: ~10 years from creation
- Session survives token refreshes indefinitely

---

## 5. API Overview

### Base URL
```
https://api.finary.com
```

### Required Headers (all API calls)
| Header | Value |
|--------|-------|
| `Authorization` | `Bearer [JWT]` |
| `Origin` | `https://app.finary.com` |
| `Referer` | `https://app.finary.com/` |
| `x-client-api-version` | `2` |
| `x-finary-client-id` | `webapp` |
| `Accept` | `*/*` |

### Response Envelope
All responses follow the same structure:
```json
{
  "result": <data>,
  "message": <string|null>,
  "error": <object|null>
}
```

### URL Structure
Most data endpoints follow this pattern:
```
/organizations/{org_id}/memberships/{membership_id}/...
```

To get org/membership IDs:
```
GET /users/me/organizations
```
Returns array of orgs, each with `members` containing `id` (membership UUID) and `member_type`.
Use the membership where `user.is_organization_owner == true`.

### ETag / Conditional Requests
- Server returns `ETag` headers (format: `W/"..."`)
- Client sends `If-None-Match` header → `304 Not Modified` responses
- Observed on 692 of 1192 requests (58%)

### Error Responses
**Zero** error responses (4xx/5xx) observed in 1192 API calls — clean traffic.

---

## 6. API Endpoints — User & Session

### `GET /users/me` (154 calls, many 304s)
Current user profile. Most frequently polled endpoint.

**Response keys:** `slug`, `firstname`, `lastname`, `fullname`, `email`, `country`, `birthdate`, `age`, `is_otp_enabled`, `access_level`, `plus_access`, `pro_access`, `subscription_status`, `ui_configuration`, `onboarding_steps`, `questionnaires`, and ~40 more fields.

### `GET /users/me/organizations` (44 calls)
List user's organizations and memberships.

**Response:** Array of orgs with nested `members[]` containing membership IDs.

**Critical for:** Extracting `org_id` and `membership_id` needed for all portfolio endpoints.

### `GET /users/me/sync_status` (67 calls)
Polling endpoint for bank sync status.

**Response keys:** `insta_sync`, `real_estates`, `investment_accounts`, `savings_accounts`, `checking_accounts`, `credit_accounts`, `cryptos`, `bank_connections`

### `GET /users/me/synchronizations` (26 calls)
Bank connection sync states.

**Query params:** `state` (comma-separated states), `sync_status` (comma-separated)

**Response item keys:** `correlation_id`, `state`, `state_message`, `connection_state`, `last_sync_at`, `last_successful_sync_at`, `sync_status`, `institution`, `provider_connection`

### `GET /users/me/subscription_details` (24 calls)
Subscription info.

**Response keys:** `subscription_status`, `subscription_platform`, `subscription_current_period_end_at`, `access_level`

### `GET /users/me/kyc_infos` (26 calls)
KYC verification status.

### `GET /users/me/notifications` (26 calls)
**Query params:** `notification_type`, `page`, `per_page`, `new_format`

### `GET /users/me/onboarding_tasks` (26 calls)
**Query params:** `task_level`

### `GET /users/me/institution_provider_incidents` (26 calls)
Active incidents affecting bank connections.

### `PUT /users/me/ui_configuration` (87 calls)
Update UI preferences (e.g., display period). Not relevant for export.

**Body:** `{ "period_display_mode": "6m" }`

### `GET /users/me/portfolio` (1 call)
**Query params:** `new_format=true`, `period`
Same as org-level portfolio but for the user directly.

### `GET /users/me/portfolio/timeseries` (1 call)
**Query params:** `new_format=true`, `period`, `timeseries_type`, `value_type`

### `GET /users/me/asset_list` (1 call)
**Query params:** `limit`, `period`

### `GET /users/me/asset_list/categories` (2 calls)
**Query params:** `with_cash_categories=true`

---

## 7. API Endpoints — Portfolio

All portfolio endpoints are under:
```
/organizations/{org_id}/memberships/{membership_id}/portfolio/...
```

### Portfolio Overview

#### `GET .../portfolio` (16 calls)
**Query params:** `new_format=true`, `period` (1d, 1w, 1m, 3m, 6m, 1y, all)

**Response keys:** `created_at`, `gross`, `net`, `finary`, `has_unqualified_loans`, `has_unlinked_loans`

#### `GET .../portfolio/timeseries` (16 calls)
**Query params:** `new_format=true`, `period`, `timeseries_type` (sum), `value_type` (gross/net)

**Response:** Array of `{ label, period_evolution_percent, timeseries, period_evolution, display_amount, display_value_difference, display_value_evolution, balance }`

#### `GET .../portfolio/fees` (4 calls)
**Query params:** `new_format=true`

**Response keys:** `total`, `data`, `is_portfolio_optimized`

#### `GET .../portfolio/insights` (2 calls)
**Response keys:** `profile`, `ranking`

#### `GET .../portfolio/dividends` (3 calls)
**Query params:** `with_real_estate=true`

**Response keys:** `annual_income`, `past_income`, `next_year`, `yield`, `past_dividends`, `upcoming_dividends`, plus per-type breakdowns (etf, fund, equity, scpi, real_estate)

#### `GET .../portfolio/geographical_allocation` (1 call)
**Response keys:** `total`, `share`, `distribution`

#### `GET .../portfolio/sector_allocation` (1 call)
**Response keys:** `total`, `share`, `distribution`

#### `GET .../portfolio/geographical_diversification` (3 calls)
**Response keys:** `score`, `analysis`

#### `GET .../portfolio/sector_diversification` (3 calls)
**Response keys:** `score`, `analysis`

#### `GET .../portfolio/geographical_suggestions` (1 call)
#### `GET .../portfolio/sector_suggestions` (1 call)

#### `GET .../portfolio/assets_leaderboard` (2 calls)
**Query params:** `mode` (absolute), `type` (etf)

**Response keys:** `leaderboard`, `insights`

### Portfolio Account Fees

#### `GET .../portfolio/accounts/fees` (2 calls)
#### `GET .../portfolio/assets/fees` (2 calls)
#### `GET .../portfolio/accounts/{account_uuid}/assets/fees` (8 calls)

---

## 8. API Endpoints — Asset Categories

Each category follows a **consistent sub-resource pattern**:

```
GET .../portfolio/{category}                    → summary (total, accounts, ownership)
GET .../portfolio/{category}/accounts           → list of accounts
GET .../portfolio/{category}/accounts/{uuid}    → single account detail
GET .../portfolio/{category}/timeseries         → historical values
GET .../portfolio/{category}/distribution       → allocation breakdown
GET .../portfolio/{category}/transactions       → paginated transactions
GET .../portfolio/{category}/dividends          → income data
GET .../portfolio/{category}/fees               → fee analysis
GET .../portfolio/{category}/geographical_allocation  → geo breakdown
GET .../portfolio/{category}/sector_allocation        → sector breakdown
```

### Categories Observed

| Category | Summary Calls | Account Calls | Has Transactions | Has Dividends | Has Fees | Has Geo/Sector |
|----------|:---:|:---:|:---:|:---:|:---:|:---:|
| `checkings` | 9 | 25 | ✅ (7) | — | — | — |
| `savings` | 10 | 26 | ✅ (5) | — | — | — |
| `investments` | 9 | 16 | ✅ (8) | ✅ (1) | ✅ (1) | ✅ |
| `real_estates` | 11 | 15 | — | ✅ (3) | ✅ (3) | ✅ |
| `cryptos` | 10 | 15 | — | — | — | — |
| `fonds_euro` | 9 | 15 | — | — | — | — |
| `commodities` | 9 | 24 | — | — | — | — |
| `credits` | 9 | 25 | ✅ (5) | — | — | — |
| `other_assets` | 10 | 25 | — | — | — | — |
| `startups` | 1 | 15 | — | — | — | — |

### Account Object Schema (Common)

All account lists share this structure:

```json
{
  "slug": "string",
  "name": "string",
  "connection_id": "uuid|null",
  "state": "string",
  "state_message": "string|null",
  "correlation_id": "uuid",
  "iban": "string|null",
  "bic": "string|null",
  "opened_at": "datetime|null",
  "id": "uuid",
  "manual_type": "string",
  "logo_url": "string",
  "created_at": "datetime",
  "annual_yield": "number",
  "balance": "number",
  "display_balance": "string",
  "organization_balance": "number",
  "display_organization_balance": "string",
  "buying_value": "number",
  "display_buying_value": "string"
}
```

### Timeseries Object Schema (Common)

```json
{
  "label": "string",
  "period_evolution_percent": "number",
  "timeseries": [{"date": "string", "value": "number"}, ...],
  "period_evolution": "number",
  "display_amount": "string",
  "display_value_difference": "string",
  "display_value_evolution": "string",
  "balance": "number"
}
```

### Distribution Object Schema (Common)

```json
{
  "total": "number",
  "distribution": [
    {
      "name": "string",
      "value": "number",
      "share": "number",
      ...
    }
  ]
}
```

---

## 9. API Endpoints — Transactions & Cashflow

### Transaction Object Schema (Common)

```json
{
  "name": "string",
  "simplified_name": "string",
  "stemmed_name": "string",
  "display_name": "string",
  "correlation_id": "uuid",
  "date": "string",
  "display_date": "string",
  "value": "number",
  "display_value": "string",
  "id": "integer",
  "transaction_type": "string",
  "commission": "number|null",
  "external_id_category": "string|null",
  "currency": "object",
  "institution": "object",
  "account": "object",
  "include_in_analysis": "boolean",
  "is_internal_transfer": "boolean",
  "marked": "boolean",
  "transaction_rule": "object|null"
}
```

### Transaction Endpoints

#### `GET .../portfolio/{category}/transactions`
**Query params:** `page` (1-based), `per_page` (default 50)

Pagination is page-based: increment `page` until result count < `per_page`.

#### `GET .../transactions` (6 calls)
Organization-level transaction search with filters.

**Query params:**
- `account_id` — Comma-separated account UUIDs
- `start_date`, `end_date` — Date range (YYYY-MM-DD)
- `period` — Period code (1m, 3m, etc.)
- `transaction_category_id` — Category filter (integer)
- `type` — Transaction type (in/out)
- `page`, `per_page`

### Cashflow Endpoints

All under `.../cashflow/...`:

#### `GET .../cashflow/configuration` (9 calls)
**Response keys:** `id`, `accounts`, `categories`

#### `GET .../cashflow/daterange` (6 calls)
**Query params:** `period`

**Response keys:** `daterange`, `first_transaction_date`, `last_transaction_date`

#### `GET .../cashflow/available_money` (7 calls)
**Query params:** `start_date`, `end_date`, `period`

#### `GET .../cashflow/distribution` (14 calls)
**Query params:** `start_date`, `end_date`, `period`, `type` (in/out), `with_subcategories=true`

**Response keys:** `display_total`, `distribution`

#### `GET .../cashflow/distribution/{category_id}` (2 calls)
Detail for a specific cashflow category.

#### `GET .../cashflow/recurring_payments` (6 calls)
**Response item keys:** `name`, `amount`, `id`, `days_frequency`, `last_payment_at`, `is_marked_as_recurring`, `confidence`, `merchant`, `transactions`, `holdings_account`

#### `GET .../cashflow/recurring_payments/distribution` (6 calls)

### Transaction Categories

#### `GET .../transaction_categories` (5 calls)
**Query params:** `included_in_analysis=true`

**Response item keys:** `id`, `name`, `color`, `icon`, `is_custom`, `main_category_id`, `target`, `should_reach_target`, `is_subcategory`, `subcategories`

---

## 10. API Endpoints — Other

### Holdings Accounts

#### `GET .../holdings_accounts` (5 calls)
**Query params:** `with_transactions=true`

Returns all accounts across categories. 19 accounts observed.

### Asset List

#### `GET .../asset_list` (1 call)
**Query params:** `limit=25`, `period`

Top assets by value. Response items include: `display_current_value`, `current_value`, `evolution`, `evolution_percent`, `unrealized_pnl`, `name`, `account_id`, `asset_id`, `asset_type`, `holding_id`, `holding_type`, `category_name`, `logo_url`, `symbol`

#### `GET .../asset_list/categories` (26 calls)
**Query params:** `with_cash_categories=true`

### Benchmarks

#### `GET .../benchmarks/available_assets` (9 calls)
**Query params:** `period`

**Response item keys:** `id`, `name`, `isin`, `url`, `period_evolution`, `period_evolution_percent`

### Real Estate Detail

#### `GET .../real_estates/{id}` (2 calls)
**Response keys:** `current_value`, `current_upnl`, `unrealized_pnl`, `evolution`, `period_evolution`, `current_price`, `buying_price`, `buying_value`

### Real Estate Account Timeseries

#### `GET .../portfolio/real_estates/accounts/{uuid}/timeseries` (2 calls)

### Financial Projection

#### `GET .../insights/financial_projection` (2 calls)
**Query params:** `duration=30`, `monthly_contribution=250`

**Response keys:** `amount`, `contribution`, `increase`, `duration`, `timeseries`

### Reference Data

#### `GET /bank_account_types` (2 calls)
**Query params:** `type` (cash, invest)

**Response item keys:** `id`, `slug`, `account_type`, `subtype`, `name`, `display_name`, `priority`

#### `GET /companies` (7 calls)
Company/institution search (likely for autocomplete).

#### `GET /holdings_account_contracts/finary` (3 calls)
Finary investment contracts.

**Response item keys:** `id`, `name`, `correlation_id`, `insurer`, `management_style`, `management_offer`, `institution`, `bank_account_type`, `holdings_account_type`, `fees`

### Invest (Finary Invest)

#### `GET /invest/v2/me` (26 calls)
**Response keys:** `fees`, `ledgers`, `total_eur_deposited`, `vip_phone_number`, `vip_status`

#### `GET /invest/v2/ledgers/{uuid}` (26 calls)
**Response keys:** `blockchain_deposit_addresses`, `company_id`, `country`, `details`, `id`, `positions`, `registration_status`, `savings_plans`, `total_eur_deposited`, `type`

---

## 11. Common Patterns

### Pagination
- **Type:** Page-based (`page` + `per_page`)
- **Default page size:** 50 (transactions), 15 (notifications)
- **Max observed:** `per_page=1000` (transactions bulk fetch)
- **Detection:** When result array length == `per_page`, fetch next page

### Period Parameter
Used extensively across portfolio endpoints:
- `1d` — 1 day
- `1w` — 1 week
- `1m` — 1 month
- `3m` — 3 months
- `6m` — 6 months
- `1y` — 1 year
- `all` — All time

### Date Ranges
Some endpoints (cashflow, transactions) accept explicit dates:
- `start_date=2026-03-01`
- `end_date=2026-03-31`

### Caching / ETags
- 58% of requests include `If-None-Match` header
- Response returns `304 Not Modified` when data hasn't changed
- Implement conditional requests to reduce bandwidth

### Organization Scoping
- Nearly all data endpoints require `org_id` and `membership_id` in the URL
- A few legacy endpoints exist under `/users/me/` (portfolio, asset_list)
- Prefer organization-scoped endpoints — they are the current API version

---

## 12. Implementation Notes

> **Note:** These were pre-implementation recommendations from API analysis. The sections below
> are annotated with what was actually implemented. See `architecture.md` for the current design.

### For the FinaryExport Tool

1. **Auth Module:** ✅ Implemented
   - Clerk auth is a standalone module (`Auth/ClerkAuthClient.cs`)
   - Uses CurlImpersonate (`CurlClient`) directly for Clerk calls (cookie control via `CookieContainer`)
   - TOTP is entered interactively via `ConsoleCredentialPrompt` (no library-based generation)
   - Token refresh runs on a background `PeriodicTimer` at 50s intervals (`TokenRefreshService`)

2. **Data Export Endpoints:** ✅ All implemented
   - `GET .../holdings_accounts` — used by `HoldingsSheet`
   - `GET .../portfolio/{category}/accounts` — used by `AccountsSheet`
   - `GET .../portfolio/{category}/transactions` — used by `TransactionsSheet` (filtered to checkings, savings, investments, credits via `AssetCategory.HasTransactions()`)
   - `GET .../portfolio` — used by `PortfolioSummarySheet`
   - `GET .../portfolio/timeseries` — used by `PortfolioSummarySheet`
   - `GET .../portfolio/dividends` — used by `DividendsSheet`

3. **Rate Limiting:** ✅ Implemented
   - No rate limit headers observed (`X-RateLimit-*` absent)
   - The webapp makes ~1192 requests in ~8 minutes (~2.5 req/s)
   - Implemented: 5 req/s token bucket (`RateLimiter`) + backoff on 429 (`FinaryDelegatingHandler`)

4. **Idempotent Export:** ✅ Confirmed
   - All export-relevant endpoints are GET (read-only)
   - The only mutating endpoint observed (`PUT /users/me/ui_configuration`) is NOT called

5. **Required Custom Headers:** ✅ Implemented in `FinaryDelegatingHandler`
   ```
   x-client-api-version: 2
   x-finary-client-id: webapp
   ```
   Without these, the API may reject requests or return different response formats.

---

*Analysis complete. Trust the wire. — Livingston*
