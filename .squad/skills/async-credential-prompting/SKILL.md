---
name: "async-credential-prompting"
description: "How to implement async credential prompting with proper error handling across multiple implementations (console, MCP elicitation, etc.)"
domain: "auth, async-patterns, error-handling"
confidence: "high"
source: "earned — fixing MCP auth sync-over-async deadlock issue"
---

## Context

When building authentication systems that need to prompt users for credentials, you may have multiple implementations (interactive console, MCP elicitation, web forms, etc.). Some are inherently sync (Console.ReadLine), others are async (network calls, IPC).

**Key principle:** Make the interface async. Don't sync-over-async in shared abstractions.

This skill applies when:
- You have a shared credential prompt interface used by multiple implementations
- At least one implementation requires async operations (MCP elicitation, web auth, IPC)
- You need to avoid deadlocks from sync-over-async wrappers like `.GetAwaiter().GetResult()`

## Patterns

### 1. Async Interface Definition

```csharp
public interface ICredentialPrompt
{
    Task<(string Email, string Password, string TotpCode)> PromptCredentialsAsync(CancellationToken ct = default);
}
```

**Why:** Even if one implementation is sync, the interface should be async if any implementation needs it. Pass `CancellationToken` to respect cancellation throughout the stack.

### 2. Sync Implementation with Task.FromResult

```csharp
public sealed class ConsoleCredentialPrompt : ICredentialPrompt
{
    public Task<(string Email, string Password, string TotpCode)> PromptCredentialsAsync(CancellationToken ct = default)
    {
        // ... synchronous Console.ReadLine() calls ...
        var email = Console.ReadLine()?.Trim() ?? "";
        var password = ReadMasked();
        var totpCode = Console.ReadLine()?.Trim() ?? "";
        
        return Task.FromResult((email, password, totpCode));
    }
}
```

**Why:** `Task.FromResult` creates a completed task with no thread blocking. No performance penalty for sync operations.

### 3. Async Implementation with Error Handling

```csharp
public sealed class McpCredentialPrompt(McpServer mcpServer) : ICredentialPrompt
{
    public async Task<(string Email, string Password, string TotpCode)> PromptCredentialsAsync(CancellationToken ct = default)
    {
        var requestParams = new ElicitRequestParams { /* schema */ };
        
        try
        {
            var result = await mcpServer.ElicitAsync(requestParams, ct);
            
            if (!result.IsAccepted || result.Content is null)
                throw new InvalidOperationException("Authentication cancelled — credentials are required.");
            
            // Extract fields from result...
            return (email, password, totpCode);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Authentication required but prompting failed. " +
                "Run the CLI first to create a session, or ensure your client supports elicitation. " +
                $"Details: {ex.Message}", ex);
        }
    }
}
```

**Why:**
- Properly async — no blocking, respects cancellation token
- Error handling distinguishes user cancellation (`InvalidOperationException`) from infrastructure failures
- Actionable error messages guide users to solutions

### 4. Caller Pattern (ColdStartAsync)

```csharp
private async Task ColdStartAsync(CancellationToken ct)
{
    logger.LogDebug("Starting cold authentication...");
    
    var (email, password, totpCode) = await credentialPrompt.PromptCredentialsAsync(ct);
    
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(totpCode))
        throw new AuthenticationException("Email, password, and TOTP code are all required.");
    
    // ... proceed with auth flow ...
}
```

**Why:** Async all the way — no sync-over-async anywhere in the call chain.

## Examples

**Project files:**
- Interface: `src/FinaryExport.Core/Auth/ICredentialPrompt.cs`
- Sync impl: `src/FinaryExport/ConsoleCredentialPrompt.cs`
- Async impl: `src/FinaryExport.Mcp/McpCredentialPrompt.cs`
- Caller: `src/FinaryExport.Core/Auth/ClerkAuthClient.cs` (line 119)

**Test mock setup:**
```csharp
var prompt = new Mock<ICredentialPrompt>();
prompt.Setup(p => p.PromptCredentialsAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(("test@example.com", "password", "123456"));
```

## Anti-Patterns

### ❌ Sync Interface with Async Implementation

```csharp
public interface ICredentialPrompt
{
    (string, string, string) PromptCredentials(); // SYNC
}

public class McpCredentialPrompt : ICredentialPrompt
{
    public (string, string, string) PromptCredentials()
    {
        // SYNC-OVER-ASYNC — risk of deadlocks!
        return ElicitCredentialsAsync().GetAwaiter().GetResult();
    }
    
    private async Task<(string, string, string)> ElicitCredentialsAsync() { ... }
}
```

**Problem:** Sync-over-async can deadlock if there's a `SynchronizationContext` (ASP.NET, WPF, etc.). Even without one, it blocks a thread unnecessarily.

### ❌ Two Interfaces (Sync + Async)

```csharp
public interface ICredentialPrompt { ... }
public interface IAsyncCredentialPrompt { ... }
```

**Problem:** Complexity for no gain. Callers need to handle both. Just make one async interface and wrap sync implementations with `Task.FromResult`.

### ❌ Generic Error Messages

```csharp
catch (Exception ex)
{
    throw new InvalidOperationException("Authentication failed", ex);
}
```

**Problem:** Doesn't guide users to action. Better: "Run CLI first to create session.dat, or ensure MCP client supports elicitation."

## Related Patterns

- **Decorator pattern with async initialization:** Ensure ALL methods that might trigger auth call `EnsureInitializedAsync()`, not just data methods. See `AutoInitFinaryApiClient` for complete coverage.
- **Error handling at boundaries:** Catch infrastructure exceptions (network, IPC) and wrap with domain exceptions that guide users to solutions.
