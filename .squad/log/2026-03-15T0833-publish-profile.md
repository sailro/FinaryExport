# Session Log: win-x64 Publish Profile

**Date:** 2026-03-15T08:33:00Z

## Summary

Linus completed win-x64 publish profile (self-contained, single-file, no trim). Tested ~90MB exe. Trimming blocked by reflection dependencies (STJ, ClosedXML, DI). Decision filed.

## Files

- `.squad/orchestration-log/2026-03-15T0833-linus.md` — orchestration record
- `.squad/decisions/inbox/linus-publish-profile.md` → decisions.md (pending merge)
- `src/FinaryExport/Properties/PublishProfiles/win-x64.pubxml` (created)
- `README.md` (updated)
