# Session Log: MCP Elicitation Fix

**Date:** 2026-03-18T13:52Z  
**Issue:** McpCredentialPrompt throwing opaque error when MCP client doesn't support elicitation

**Fix:** 
- Added pre-flight capability check (`EnsureElicitationSupported()`)
- Fixed exception filter that was silently swallowing the critical error
- Improved error message to guide users to fallback (run CLI first)

**Result:** Build clean, 240/240 tests passing. No regressions.

**File:** `src/FinaryExport.Mcp/McpCredentialPrompt.cs`
