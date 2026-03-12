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
- **Key cookies:** `__client` (session), `__client_uat`, `__client_uat_f7YV-REf` — must persist in cookie jar.
- **API base:** `https://api.finary.com`, 105 unique endpoints cataloged, zero errors observed.
- **Required custom headers:** `x-client-api-version: 2`, `x-finary-client-id: webapp`
- **Response envelope:** All responses: `{ result, message, error }`
- **URL pattern:** `/organizations/{org_id}/memberships/{membership_id}/...` — IDs from `GET /users/me/organizations`
- **Pagination:** Page-based (`page`, `per_page`), max observed per_page=1000.
- **Asset categories:** checkings, savings, investments, real_estates, cryptos, fonds_euro, commodities, credits, other_assets, startups. Each has consistent sub-resource pattern (accounts, timeseries, distribution, transactions).
- **ETag caching:** 58% of requests use conditional `If-None-Match`.
- **Analysis output:** `C:\Dev\FinaryExport\api-analysis.md`
