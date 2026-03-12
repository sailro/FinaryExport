# Team Decisions

## Decision: Clerk Auth Flow & API Protocol Details

**By:** Livingston (Protocol Analyst)  
**Date:** 2026-03-12  
**Scope:** Auth implementation, API client design  

### Auth Architecture

Finary uses **Clerk** (clerk.finary.com) for authentication. The login is a 6-step flow:
1. GET `/v1/environment` — config
2. GET `/v1/client` — establish client cookies
3. POST `/v1/client/sign_ins` — email + password (form-encoded)
4. POST `/v1/client/sign_ins/{id}/attempt_second_factor` — TOTP code
5. POST `/v1/client/sessions/{id}/touch` — activate session
6. POST `/v1/client/sessions/{id}/tokens` — get JWT

**2FA is mandatory** (TOTP enabled). The tool will need a TOTP secret from the user to generate codes autonomously.

### Token Lifecycle

- JWT is RS256, 60-second TTL
- Refresh via POST to `/tokens` endpoint every ~50 seconds
- Refresh uses `__client` cookie (long-lived), not the JWT itself
- Session lives ~90 days without re-login

### API Protocol

- Base URL: `https://api.finary.com`
- Required headers: `x-client-api-version: 2`, `x-finary-client-id: webapp`
- Response envelope: `{ result, message, error }`
- Endpoints scoped to `/organizations/{org_id}/memberships/{membership_id}/...`
- Pagination: `page` + `per_page` (page-based, 1-indexed)

### Implications for Implementation

- Auth module needs: HttpClient with CookieContainer, TOTP generator, background token refresh timer
- All Clerk calls need `Origin: https://app.finary.com` and query params `__clerk_api_version=2025-11-10&_clerk_js_version=5.125.4`
- Full analysis at `api-analysis.md`
