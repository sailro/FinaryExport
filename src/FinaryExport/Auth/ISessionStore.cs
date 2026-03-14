namespace FinaryExport.Auth;

// Abstracts persistence of session data (session ID + cookies).
// Enables warm start by restoring cookies and session ID between runs.
public interface ISessionStore
{
	// Persists session data (encrypted at rest).
	Task SaveSessionAsync(SessionData data, CancellationToken ct = default);

	// Loads previously saved session data.
	// Returns null if no session exists or if the stored data is corrupted/expired.
	Task<SessionData?> LoadSessionAsync(CancellationToken ct = default);

	// Clears persisted session (e.g., on auth failure / 401 from warm start).
	Task ClearSessionAsync(CancellationToken ct = default);
}
