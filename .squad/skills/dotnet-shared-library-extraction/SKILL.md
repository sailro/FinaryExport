---
name: "dotnet-shared-library-extraction"
description: "Extract shared code from a .NET Exe project into a class library for multi-project reuse"
domain: "architecture"
confidence: "high"
source: "earned: FinaryExport MCP architecture proposal"
---

## Context

When a .NET solution needs to share code between multiple executable projects (CLI, MCP server, web API), extract the shared code into a class library. This avoids referencing an Exe project (which drags in all its dependencies) and creates a clean dependency graph.

## Patterns

1. **Keep the original RootNamespace on the new library.** If the original project was `FinaryExport` with namespace `FinaryExport`, set `<RootNamespace>FinaryExport</RootNamespace>` on the new `.Core` library. This means zero `using` statement changes across the entire codebase — files physically move, namespaces don't.

2. **Split ServiceCollectionExtensions.** The original `AddFinaryExport()` becomes `AddFinaryCore()` in Core (registers only shared services). Each consuming project registers its own specializations (e.g., `ICredentialPrompt` implementation).

3. **Interface stays in Core, implementations split by consumer.** `ICredentialPrompt` in Core. `ConsoleCredentialPrompt` in CLI. `EnvCredentialPrompt` in MCP. Each host decides how credentials are collected.

4. **Packages follow the code.** If `CurlImpersonate` is used by Core code, the PackageReference goes on Core. Consuming projects get it transitively. Don't duplicate PackageReferences.

5. **Validate with existing tests first.** After extraction, the CLI project must build clean and all tests must pass before writing any new code. This is a refactoring gate.

## Examples

```
Before:  FinaryExport (Exe) → has everything
After:   FinaryExport.Core (Library) → auth, api, models, infrastructure
         FinaryExport (Exe) → CLI, export, console prompts → references Core
         FinaryExport.Mcp (Exe) → MCP server, tools, env prompts → references Core
```

## Anti-Patterns

- Don't reference an Exe project from another Exe. It compiles but drags in unwanted dependencies (ClosedXML in an MCP server, System.CommandLine in a web API).
- Don't change namespaces during extraction. The churn is enormous and the risk of breakage is high for zero benefit.
- Don't put consumer-specific implementations in Core (e.g., `ConsoleCredentialPrompt` has no business in a library that MCP references).
