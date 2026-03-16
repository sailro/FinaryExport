# Squad Team

> FinaryExport — .NET 10 tool to export wealth data from Finary to xlsx

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Rusty | Lead | `.squad/agents/rusty/charter.md` | 🏗️ Active |
| Linus | Backend Dev | `.squad/agents/linus/charter.md` | 🔧 Active |
| Livingston | Protocol Analyst | `.squad/agents/livingston/charter.md` | 🔒 Active |
| Basher | Tester | `.squad/agents/basher/charter.md` | 🧪 Active |
| Saul | MCP Specialist | `.squad/agents/saul/charter.md` | ⚙️ Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Active |
| Ralph | Work Monitor | — | 🔄 Monitor |

## Project Context

- **Owner:** the user
- **Project:** FinaryExport — a .NET 10 tool that exports data from Finary (wealth management platform) to xlsx files. Reverse-engineers Finary's API from captured HTTP traffic and replicates authentication autonomously.
- **Stack:** .NET 10, C#, httpproxymcp (traffic capture), xlsx export
- **Key domains:** Institutions, accounts, assets, transactions
- **Auth:** Fully autonomous — no shared cookies/session tokens. Tool handles full login/token lifecycle.
- **Created:** 2026-03-12
