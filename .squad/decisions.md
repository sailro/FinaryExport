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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
