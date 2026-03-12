# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Learnings

### Auth Module Requirements (2026-03-12)
Finary uses Clerk authentication with mandatory TOTP 2FA. Auth flow is 6-step process:
1. GET `/v1/environment` → fetch config
2. GET `/v1/client` → establish session
3. POST `/v1/client/sign_ins` → email + password
4. POST `/v1/client/sign_ins/{id}/attempt_second_factor` → TOTP validation
5. POST `/v1/client/sessions/{id}/touch` → activate
6. POST `/v1/client/sessions/{id}/tokens` → get JWT (RS256, 60s TTL)

**Key Implementation Notes:**
- JWT refresh every ~50 seconds via POST `/tokens` (uses persistent `__client` cookie, not JWT)
- Clerk calls require `Origin: https://app.finary.com` header
- Query params: `__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4`
- HttpClient must use CookieContainer for `__client` cookie persistence
- Session duration: ~90 days without re-login

**Dependencies:** Need TOTP generator library (e.g., OtpNet for .NET)
