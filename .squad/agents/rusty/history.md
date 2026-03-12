# Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (a wealth management platform) to xlsx files. The tool reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously (no shared cookies/session tokens).
- **Stack:** .NET 10, C#, HTTP client, xlsx generation, httpproxymcp for traffic capture
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth goal:** Fully autonomous auth — user provides credentials, tool handles login/token lifecycle independently
- **Created:** 2026-03-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-03-12 — Architecture defined.** Wrote `architecture.md` as the implementation blueprint. Key decisions:
  - Single project, namespace separation (not multi-project). CLI tool doesn't warrant assembly overhead.
  - ClosedXML for xlsx (MIT, active). EPPlus rejected (commercial license).
  - No Polly — retry logic is trivial for a run-once tool. Hand-rolled in delegating handler.
  - `ITokenProvider` is the only auth surface visible to the rest of the app. Decouples from Clerk internals.
  - One `FinaryApiClient` split into partial classes by concern. Category endpoints are parameterized by enum, not separate clients.
  - `record` types, `decimal` for money, STJ source generators with `SnakeCaseLower`.
  - Per-category error isolation: one failing category doesn't kill the export.
  - `PeriodicTimer`-based token refresh as `IHostedService` (50s interval, JWT TTL is 60s).
  - Credentials via env vars / user secrets. Never in `appsettings.json`.
  - Generic host (`Host.CreateApplicationBuilder`) for DI, config, logging, hosted services.
- **Key file paths:** `architecture.md` (blueprint), `api-analysis.md` (protocol reference)
- **Owner preference:** the user wants opinionated decisions, not options lists. Direct and decisive.
- **2026-03-12 — Session persistence (D13).** Added `ISessionStore` + `EncryptedFileSessionStore` to architecture. Two-tier auth: warm start (load persisted `__client` cookie → `/tokens` → JWT) tried first, cold start (full 6-step) as fallback. DPAPI encryption at rest. Non-fatal — store failures never block auth. New config: `SessionStorePath`, `--clear-session`. `ITokenProvider` API unchanged — no impact outside Auth/.
