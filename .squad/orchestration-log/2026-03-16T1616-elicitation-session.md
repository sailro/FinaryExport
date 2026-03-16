# Orchestration Log: MCP Elicitation Debugging Session

**Date:** 2026-03-16  
**Time:** 16:16 UTC  
**Session Focus:** Fixing broken credential elicitation in MCP server  
**Final Commit:** 5d2722a "fix: MCP elicitation credential prompt"

## Session Overview

Extended debugging session to resolve complete authentication failure in MCP tools. Users could not authenticate — every tool call requiring credentials returned generic "An error occurred" error with no details.

## Spawn Timeline

### Spawn 1: Pre-flight Check & Capability Backfill
- **Purpose:** Address elicitation capability negotiation
- **Output:** Added pre-flight capability check and FormElicitationCapability backfill
- **Result:** HELPFUL BUT NOT THE FIX — elicitation still failed
- **Impact:** Kept in final solution for robustness

### Spawn 2: Capability Negotiation Investigation
- **Purpose:** Verify capability type definitions and negotiation logic
- **Output:** Confirmed types exist, capability negotiation working
- **Result:** Useful research but not the root cause
- **Impact:** Confirmed elicitation capability was properly defined

### Spawn 3: Session Persistence Investigation
- **Purpose:** Investigate whether lack of session persistence was the issue
- **Result:** INVESTIGATED WRONG PROBLEM — session persistence wasn't the issue
- **Impact:** May have introduced incorrect modifications

## Root Cause Discovery Process

1. **Iteration 1 — Assumption Phase:**
   - Initial theory: Copilot CLI doesn't support elicitation
   - Investigation confirmed: Copilot CLI DOES support elicitation
   - **Conclusion:** Problem is in server-side elicitation setup

2. **Iteration 2 — Capability Backfill Phase:**
   - Added pre-flight capability check
   - Added FormElicitationCapability backfill
   - **Result:** Still failing, but now with slightly better capability negotiation

3. **Iteration 3 — Debug Tool Phase:**
   - Built debug_elicitation tool to call ElicitAsync directly
   - **BREAKTHROUGH:** debug tool worked! Users could authenticate through it
   - **Key Question:** Why does debug work but real tools fail?

4. **Iteration 4 — Exception Surfacing Phase:**
   - Wrapped get_profiles in try/catch
   - Surfaced the REAL exception instead of swallowing it
   - **CRITICAL FINDING:** ArgumentException from MCP SDK
   - Message: Invalid Format value in StringSchema

5. **Iteration 5 — Root Cause Identified:**
   - `Format = "password"` violates MCP SDK constraints
   - SDK only allows: email, uri, date, date-time
   - Removed Format="password" from password field
   - **FIX CONFIRMED:** Elicitation now works

## Actions Taken

### Code Changes
- Removed `Format = "password"` from ElicitRequestParams password field schema
- Removed debug_elicitation tool (temporary debugging aid)
- Removed verbose error wrapping (surfaced exception was only needed to find root cause)
- Cleaned up temporary logging statements

### Documentation & Decisions
- Recorded root cause analysis in decision document
- Updated architectural knowledge: MCP elicitation works with Copilot CLI
- Documented SDK constraints for future developers
- Recorded lesson learned: always surface exceptions for debugging

## Key Insight

The critical breakthrough was asking "why does the debug tool work but the real tools fail?" This led to:
1. Wrapping get_profiles in try/catch to surface exceptions
2. Discovering the ArgumentException from MCP SDK
3. Identifying the Format="password" constraint violation
4. The one-line fix

## Session Impact

- **Severity:** CRITICAL — authentication completely broken
- **Resolution:** One line of code removed
- **Root Cause Simplicity:** Paradoxically simple (invalid format value) but hard to discover without exception visibility
- **Learning:** Exception wrapping and debugging tools are essential for identifying silent failures

## Subsequent Tasks

Scribe is recording:
1. Full decision in decisions/inbox
2. Session log in log/
3. Update Saul's history.md with correct root cause
4. Check for stale decision files from earlier spawns
5. Git commit all .squad/ changes
