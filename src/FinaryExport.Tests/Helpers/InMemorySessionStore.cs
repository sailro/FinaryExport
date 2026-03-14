using System.Net;
using FinaryExport.Auth;

namespace FinaryExport.Tests.Helpers;

// In-memory ISessionStore for testing. No file system, no encryption.
public sealed class InMemorySessionStore : ISessionStore
{
	private SessionData? _session;
	public int SaveCount { get; private set; }
	public int LoadCount { get; private set; }
	public int ClearCount { get; private set; }
	public bool ThrowOnLoad { get; set; }
	public bool ThrowOnSave { get; set; }

	public Task SaveSessionAsync(SessionData data, CancellationToken ct = default)
	{
		SaveCount++;
		if (ThrowOnSave) throw new IOException("Simulated disk failure");
		_session = data;
		return Task.CompletedTask;
	}

	public Task<SessionData?> LoadSessionAsync(CancellationToken ct = default)
	{
		LoadCount++;
		return ThrowOnLoad
			? throw new IOException("Simulated disk failure")
			: Task.FromResult(_session);
	}

	public Task ClearSessionAsync(CancellationToken ct = default)
	{
		ClearCount++;
		_session = null;
		return Task.CompletedTask;
	}

	// Seed session data for warm start scenarios.
	public void Seed(SessionData data) => _session = data;

	// Convenience overload: seed with cookies and a default session ID.
	public void Seed(IReadOnlyCollection<Cookie> cookies, string sessionId = "sess_test") =>
		_session = new SessionData(sessionId, cookies);
}
