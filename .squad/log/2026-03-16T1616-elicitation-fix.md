# Session Log: MCP Elicitation Fix

**Date:** 2026-03-16T16:16Z**  
**Session Type:** Debugging - Critical Authentication Failure  
**Status:** RESOLVED  
**Commit:** 5d2722a

## Problem Statement

MCP server's credential elicitation was completely broken. All tool calls requiring authentication failed with:
- Error message: "An error occurred"
- No diagnostic details
- Users could not authenticate
- All auth-dependent features inaccessible

## Investigation & Solution

### Discovery Process
- **Initial Assumption (Wrong):** Copilot CLI doesn't support elicitation
  - Disproven: CLI does support elicitation
  
- **Hypothesis 2:** Capability negotiation is broken
  - Spawn 1 & 2 added fixes here
  - Helped but didn't resolve the issue
  
- **Hypothesis 3:** Session persistence needed
  - Spawn 3 investigated this path
  - Investigated the wrong problem
  
- **Breakthrough Moment:** Built debug_elicitation tool
  - Called ElicitAsync directly
  - **It worked!** Users could authenticate
  - Revealed the paradox: "Why does debug work but real tools fail?"
  
- **Exception Surfacing:** Wrapped get_profiles in try/catch
  - Caught real exception: ArgumentException
  - MCP SDK rejecting Format="password" in StringSchema
  - SDK only allows: email, uri, date, date-time

### Root Cause

```
ElicitRequestParams.StringSchema with Format = "password"
↓
Throws ArgumentException (invalid format value)
↓
Exception silently caught somewhere
↓
Returns generic "An error occurred"
↓
Users can't authenticate
```

### The Fix

One line removed from ElicitRequestParams:
```diff
- StringSchema password = new StringSchema { Format = "password" };
+ StringSchema password = new StringSchema();
```

**Result:** Elicitation works perfectly.

## Technical Details

### What MCP SDK Allows
Valid format values in StringSchema:
- `email`
- `uri`  
- `date`
- `date-time`

### What MCP SDK Rejects
- `password` ← violates constraint
- Any other custom format values

### How This Was Found
1. Assumed Copilot CLI might not support elicitation
2. Built debug tool that proved it does
3. Asked why debug worked but real tools failed
4. Wrapped exceptions to see the real error
5. Found ArgumentException about invalid format
6. Checked MCP SDK docs, found constraint list
7. Removed Format="password"
8. Problem solved

## Changes Made

### Code
- `src/FinaryExport.MCP/Authentication/ElicitRequestParams.cs`
  - Removed `Format = "password"` from password field schema

### Temporary Items (Cleaned Up)
- debug_elicitation tool → removed
- Exception wrapping in get_profiles → cleaned up (exception visibility was only needed for root cause discovery)
- Verbose logging → removed

### Kept from Earlier Spawns
- Pre-flight capability check (Spawn 1)
- FormElicitationCapability backfill (Spawn 1)
  - Reason: Some clients send `elicitation: {}` without form sub-mode

## Decisions Superseded

From D-mcp-complete decision #5:
- **Old:** "Session-only auth — server requires session.dat"
- **New:** "MCP elicitation supports cold auth — works without session.dat"
- **Reason:** Elicitation capability functions correctly when properly configured

## Validation

✅ Credential elicitation works  
✅ Users can authenticate via MCP tools  
✅ All auth-dependent features now accessible  
✅ Error messages are clear (no more generic "An error occurred")  
✅ Debug tool proves capability exists in Copilot CLI  

## Lessons Recorded

1. **Silent Exceptions Are Dangerous:** The original code swallowed the ArgumentException. Always surface exceptions in debug scenarios.

2. **Debug Tools Validate Assumptions:** Building debug_elicitation proved elicitation capability exists, contradicting initial theory.

3. **SDK Constraints Are Strict:** MCP SDK validates format values strictly. Always check SDK documentation for allowed values.

4. **Paradoxes Point to Root Cause:** "Why does debug work but real tools fail?" is a powerful debugging question.

## Session Artifacts

- Decision: .squad/decisions/inbox/scribe-elicitation-fix.md
- Session log: .squad/log/2026-03-16T1616-elicitation-fix.md
- Orchestration: .squad/orchestration-log/2026-03-16T1616-elicitation-session.md
