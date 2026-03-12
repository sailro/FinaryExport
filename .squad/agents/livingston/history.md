# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-12 — API Analysis Complete

- **Auth provider:** Clerk (clerk.finary.com), not custom. Clerk JS SDK v5.125.4, API version 2025-11-10.
- **Auth flow:** 6-step: environment → client → sign_in (email+password) → 2FA (TOTP) → session touch → get JWT token.
- **2FA is enabled:** Account uses TOTP. Autonomous auth needs TOTP secret for code generation.
- **JWT details:** RS256, 60-second TTL, ~766 chars. Claims: azp, exp, fva, iat, iss, nbf, sid, sts, sub. Issuer: `https://clerk.finary.com`.
- **Token refresh:** POST `/v1/client/sessions/{session_id}/tokens` every ~46s. Uses `__client` cookie, not old JWT.
- **Key cookies:** `__client` (session), `__client_uat`, `__client_uat_{instance}` — must persist in cookie jar.
- **API base:** `https://api.finary.com`, 105 unique endpoints cataloged, zero errors observed.
- **Required custom headers:** `x-client-api-version: 2`, `x-finary-client-id: webapp`
- **Response envelope:** All responses: `{ result, message, error }`
- **URL pattern:** `/organizations/{org_id}/memberships/{membership_id}/...` — IDs from `GET /users/me/organizations`
- **Pagination:** Page-based (`page`, `per_page`), max observed per_page=1000.
- **Asset categories:** checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups. Each has consistent sub-resource pattern (accounts, timeseries, distribution, transactions).
- **ETag caching:** 58% of requests use conditional `If-None-Match`.
- **Analysis output:** `C:\Dev\FinaryExport\api-analysis.md`

### 2026-03-12 — Client 429 Root Cause (Cloudflare Bot Management)

- **Root cause:** The 429 on `GET /v1/client` is Cloudflare bot management, NOT Clerk rate limiting.
- **Why `/v1/environment` works:** Cloudflare caches it (`cf-cache-status: HIT`), bypassing bot checks. `/v1/client` is `DYNAMIC` — every request goes through the full Cloudflare pipeline.
- **Missing from our tool:**
  - **Cloudflare cookies:** `cf_clearance` (from JS challenge on `finary.com`), `__cf_bm`, `_cfuvid` — all Domain=`finary.com`, sent with all `*.finary.com` requests.
  - **Browser fingerprint headers:** User-Agent (we send default .NET), `sec-ch-ua`, `sec-ch-ua-mobile`, `sec-ch-ua-platform`, `sec-fetch-dest`, `sec-fetch-mode`, `sec-fetch-site`, `accept-language`.
- **Fix strategy (3 tiers):**
  1. Add full browser headers to `ClerkDelegatingHandler` (~10 lines) — may suffice.
  2. Add "Cloudflare warmup" GET to `finary.com` to obtain `__cf_bm`/`_cfuvid` cookies.
  3. Headless browser bootstrap if `cf_clearance` is required (nuclear option).
- **Investigation output:** `C:\Dev\FinaryExport\client-429-investigation.md`

### 2026-03-12 — CurlImpersonate: The Actual Cloudflare Bypass

- **All three header/cookie tiers failed.** Adding browser headers, Cloudflare cookie warmup, and even full CookieContainer sharing was insufficient — Cloudflare's bot management detects TLS fingerprints, not just HTTP-level signals.
- **Root cause confirmed:** .NET's `SslStream` produces a TLS ClientHello with cipher suites, extensions, and ordering distinct from any real browser. Cloudflare fingerprints this at the TLS layer before HTTP headers are even parsed.
- **Solution:** `Loxifi.CurlImpersonate` (1.1.0) — .NET wrapper around curl-impersonate, which produces byte-identical Chrome TLS handshakes using `BrowserProfile.Chrome136`.
- **Architecture impact:**
  - ClerkAuthClient uses `CurlClient` directly (not HttpClientFactory) for all Clerk API calls — CurlClient manages its own cookie jar and TLS stack.
  - New `CurlMessageHandler` bridges CurlClient into `HttpMessageHandler` for Finary API calls, preserving the HttpClientFactory + DelegatingHandler pipeline.
  - All three fix tiers from the 429 investigation became unnecessary — CurlImpersonate subsumes them.
- **Verification:** Both cold start and warm start auth flows succeed without any 403/429. API data export works end-to-end.
- **Lesson:** When fighting Cloudflare bot management on a reverse-engineered API, TLS fingerprint impersonation is the correct layer to operate at. HTTP headers are table stakes but insufficient.
