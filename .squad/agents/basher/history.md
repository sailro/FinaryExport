# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Learnings

### TOTP 2FA Requirement (2026-03-12)
Finary login requires TOTP 2-factor authentication. This is not optional—all user accounts have TOTP enabled.

**Flow:**
1. After email/password submission, Finary returns `sign_in_id`
2. User must provide TOTP code (6-digit, time-based)
3. POST `/v1/client/sign_ins/{id}/attempt_second_factor` with TOTP code
4. Then proceed to session activation (`/sessions/{id}/touch`) and token retrieval

**Implementation Impact:**
- User must be prompted to provide TOTP secret (33-character base32 string) during setup
- Tool needs TOTP code generation library (e.g., OtpNet for .NET)
- Generate codes on-demand during login (time-based, 30-second window)
- No static codes possible—must be generated autonomously

**Scope:** Essential for autonomous authentication

### Architecture Blueprint (2026-03-12)

**From Rusty (Lead):** Architecture document finalized in `architecture.md`. Key decisions:

- **Per-category error isolation** — one failing category (e.g., crypto portfolio) cannot kill the entire export. Tests must verify category isolation.
- **ITokenProvider abstraction** — single auth interface for all token operations. Auth module is the only place Clerk is known. Enables testing with mock providers and future auth swaps.
- **PeriodicTimer (50s) token refresh** as `IHostedService` — autonomous background service. Basher tests should verify token refresh doesn't interfere with concurrent API calls.

**Impact on Basher:** Test category failure isolation extensively (one category failure must not break others). Mock ITokenProvider for all auth tests. Verify PeriodicTimer behavior under load and concurrent category exports.

