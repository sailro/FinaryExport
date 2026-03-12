# Squad Decisions

## Active Decisions

### 2026-03-12: FinaryExport Architecture Blueprint

**Author:** Rusty (Lead)  
**Scope:** Entire project  

Architecture document: `architecture.md`

**Key Decisions:**
1. **Single project** — namespace separation, not multi-project. This is a CLI tool.
2. **ClosedXML** for xlsx — MIT, active. EPPlus rejected (commercial).
3. **No Polly** — retry logic hand-rolled. Too simple to justify a dependency.
4. **`ITokenProvider`** — sole auth abstraction. Nothing outside Auth/ knows about Clerk.
5. **One `FinaryApiClient`** — partial classes by concern, category endpoints parameterized by enum.
6. **`record` types, `decimal` for money, STJ source generators, `SnakeCaseLower`.**
7. **Per-category error isolation** — one failing category cannot kill the export.
8. **`PeriodicTimer` token refresh** as `IHostedService` (50s interval).
9. **Credentials via env vars / user secrets only.** Never in appsettings.json.
10. **Generic host** (`Host.CreateApplicationBuilder`) for DI, config, logging.

**Impact:** All team members implementing code follow this architecture. Linus builds from here.

---

### 2026-03-12: User Directive — Premium Models for Agent Spawns

**By:** the user (via Copilot)

Always use premium models for agent spawns — no cost-tier restrictions.

---

### 2026-03-12: Session Persistence via ISessionStore (D13)

**Author:** Rusty (Lead)  
**Scope:** Auth Module

Add cookie/session persistence to skip the full 6-step Clerk auth on subsequent runs.

**Key Points:**
1. **`ISessionStore` interface** — `SaveSessionAsync`, `LoadSessionAsync`, `ClearSessionAsync`. Abstracts storage; default implementation uses DPAPI-encrypted file.
2. **Two-tier auth flow** — Warm start (load cookie → `/tokens` → JWT) tried first. Cold start (full 6-step) as fallback on 401 or missing session.
3. **Encrypted at rest** — DPAPI (`DataProtectionScope.CurrentUser`) on Windows. `IDataProtectionProvider` as cross-platform fallback.
4. **Non-fatal** — Session store failures never block auth. Missing/corrupted store → cold start. Failed save → warning only.
5. **Configurable** — `SessionStorePath` in `FinaryOptions` (default: `~/.finaryexport/session.dat`). `--clear-session` CLI flag forces cold start.
6. **Architecture doc updated** — `architecture.md` revised with Auth Module section, project structure, configuration, execution flow, error handling.

**Rationale:** The `__client` cookie has ~90-day expiry and survives token refreshes. Persisting it eliminates 5 of 6 auth requests on every run after the first.

**Impact:** Linus implements `ISessionStore.cs`, `EncryptedFileSessionStore.cs`. `ClerkAuthClient` gains warm start logic. Auth API (`ITokenProvider`) unchanged.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
