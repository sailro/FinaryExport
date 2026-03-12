# Project Context

- **Project:** FinaryExport — .NET 10 CLI tool exporting Finary wealth management data to xlsx
- **Created:** 2026-03-12

## Core Context

Scribe maintains squad state: agent histories, decisions registry, session logs. All `.squad/` files must reflect the actual codebase, not plans or intentions.

## Sessions Logged

### 2026-03-12 — Full Development Session (Retroactive)

**Scope:** Entire development lifecycle from API analysis to working multi-profile export.

**Key events (chronological):**
1. **Livingston** analyzed captured API traffic → documented Clerk auth flow (6-step), API catalog (105 endpoints), rate patterns, asset categories
2. **Livingston** investigated Clerk 429s → identified Cloudflare bot management as root cause (TLS fingerprint detection, not just missing headers)
3. **Rusty** produced architecture blueprint (D1-D10): single project, ClosedXML, ITokenProvider abstraction, partial class FinaryApiClient, Generic Host
4. **Rusty** designed session persistence (D13): ISessionStore + EncryptedFileSessionStore, DPAPI encryption, two-tier warm/cold auth
5. **Linus** built initial implementation: 49 files, 6-step Clerk auth, typed API client, 4 sheet writers (Summary, Accounts, Transactions, Dividends)
6. **Basher** created test suite: 94 tests, 13 test files, contract stubs → reconciled against live implementation
7. **Linus** rewrote auth to interactive prompts (removed stored credentials, added ICredentialPrompt)
8. **Linus** attempted Cloudflare bypass (browser headers + cookie warmup) — failed due to TLS fingerprinting
9. **Linus** adopted CurlImpersonate (D-curl): `Loxifi.CurlImpersonate` 1.1.0 with `BrowserProfile.Chrome136` — full Cloudflare bypass
10. **Linus** added models: SecurityPosition, SecurityInfo, HoldingsAccount, OwnershipEntry, FinaryProfile, AssetCategory enum
11. **Linus** added HoldingsSheet (security-level export with ISIN, symbol, quantity, prices, P&L)
12. **Linus** built multi-profile export (D-multiprofile): one xlsx per membership + unified aggregated file
13. **Linus** built UnifiedFinaryApiClient (D-unified): decorator pattern, cross-membership aggregation, shared asset scaling
14. **Linus** removed dead code: ClerkDelegatingHandler, FinaryJsonContext, unused Auth models, AccountDetail, Otp.NET
15. **Basher** expanded tests to 134 (4 new test files: ExportContext, Holdings, DelegatingHandler, UnifiedApiClient)
16. PII scrub applied (D-pii): all real names replaced with Jean/Marie/Claire Dupont

**Decisions recorded this session:** D-curl, D-multiprofile, D-unified, D-pii, D-ratelimit, D-logging, D-noxml

**Final state:** Working CLI tool with multi-profile export. 134 tests passing. Auth via CurlImpersonate. 5 sheet types.

## Learnings

- Squad state must be updated during the session, not retroactively. When agents spawn directly (bypassing orchestration), the Scribe has no opportunity to log in real time — resulting in this bulk catchup.
- PII rule: never use real names in `.squad/` files. Use "the user" or synthetic names (Jean/Marie/Claire Dupont).
- The decisions registry (`decisions.md`) is the single source of truth for architectural choices. Agent histories should reference decisions by ID, not duplicate rationale.
