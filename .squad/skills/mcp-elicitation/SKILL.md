---
name: "mcp-elicitation"
description: "How to safely use MCP Elicitation (ElicitAsync) in .NET MCP servers"
domain: "mcp, error-handling"
confidence: "high"
source: "earned — diagnosed and fixed broken elicitation flow"
---

## Context

MCP Elicitation lets a server request structured user input from the client (host). The client must advertise the capability during `initialize`. If the client doesn't support it, the SDK throws `InvalidOperationException` — which is easy to miss in catch filters.

## Patterns

1. **Always pre-check client capabilities before calling ElicitAsync:**
   ```csharp
   if (mcpServer.ClientCapabilities?.Elicitation?.Form is null)
       throw new InvalidOperationException("Client does not support elicitation.");
   ```

2. **ElicitRequestParams.Mode defaults to `"form"` in SDK 1.1.0** — no need to set it explicitly for form-mode elicitation.

3. **Elicitation is client-advertised, not server-declared.** `ServerCapabilities` has no elicitation property. The client sends `{ capabilities: { elicitation: { form: {} } } }` during `initialize`.

4. **SDK throws `InvalidOperationException`, not `McpException`** for capability checks — handle accordingly.

## Examples

```csharp
// Good: pre-flight check + defensive catch
private bool IsElicitationSupported() =>
    mcpServer.ClientCapabilities?.Elicitation?.Form is not null;

public async Task<T> PromptAsync(CancellationToken ct)
{
    if (!IsElicitationSupported())
        throw new InvalidOperationException("Client doesn't support elicitation. Workaround: ...");

    try { return await mcpServer.ElicitAsync(params, ct); }
    catch (InvalidOperationException) when (!IsElicitationSupported()) { /* re-wrap */ }
    catch (Exception ex) when (ex is not InvalidOperationException) { /* other failures */ }
}
```

## Anti-Patterns

1. **Don't use `catch (Exception ex) when (ex is not InvalidOperationException)`** as a catch-all — SDK capability errors ARE `InvalidOperationException` and will slip through.

2. **Don't call ElicitAsync without checking capabilities first** — the SDK throws internally, but the error message is generic and not actionable for users.

3. **Don't assume all MCP hosts support elicitation** — it's an optional client capability. Always provide a fallback or workaround path.
