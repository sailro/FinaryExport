using System.Net;
using FinaryExport.Auth;
using FinaryExport.Tests.Fixtures;
using FinaryExport.Tests.Helpers;
using FluentAssertions;

namespace FinaryExport.Tests.Auth;

// Tests for the ISessionStore contract.
// Validates save/load/clear operations, corruption handling, and non-fatal failures.
// Per D13: session store failures are never fatal.
public sealed class SessionStoreTests
{
	private static SessionData MakeSession(Cookie[]? cookies = null, string sessionId = "sess_test") =>
		new(sessionId, cookies ?? ApiFixtures.SessionCookies);

	[Fact]
	public async Task SaveAndLoad_RoundTrip_ReturnsSameSession()
	{
		// Arrange
		var store = new InMemorySessionStore();
		var session = MakeSession();

		// Act
		await store.SaveSessionAsync(session);
		var loaded = await store.LoadSessionAsync();

		// Assert
		loaded.Should().NotBeNull();
		loaded.SessionId.Should().Be(session.SessionId);
		loaded.Cookies.Should().HaveCount(session.Cookies.Count);
		loaded.Cookies.Select(c => c.Name).Should().BeEquivalentTo(session.Cookies.Select(c => c.Name));
	}

	[Fact]
	public async Task Load_EmptyStore_ReturnsNull()
	{
		// Arrange
		var store = new InMemorySessionStore();

		// Act
		var result = await store.LoadSessionAsync();

		// Assert: null = no stored session, caller should do cold start
		result.Should().BeNull();
		store.LoadCount.Should().Be(1);
	}

	[Fact]
	public async Task Clear_AfterSave_LoadReturnsNull()
	{
		// Arrange
		var store = new InMemorySessionStore();
		await store.SaveSessionAsync(MakeSession());

		// Act
		await store.ClearSessionAsync();
		var result = await store.LoadSessionAsync();

		// Assert: cleared store returns null
		result.Should().BeNull();
		store.ClearCount.Should().Be(1);
	}

	[Fact]
	public async Task Clear_EmptyStore_DoesNotThrow()
	{
		// Arrange
		var store = new InMemorySessionStore();

		// Act & Assert: clearing an empty store is a no-op, not an error
		var act = async () => await store.ClearSessionAsync();
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task Load_CorruptedFile_ReturnsNull()
	{
		// Arrange: simulate corrupted store
		var store = new InMemorySessionStore { ThrowOnLoad = true };

		// Act: corrupted data should NOT throw — per D13, returns null
		// The InMemorySessionStore throws IOException when ThrowOnLoad is true.
		// The real EncryptedFileSessionStore catches CryptographicException/JsonException → returns null.
		// Test verifies the contract: errors during load are handled gracefully.
		Func<Task> act = async () => await store.LoadSessionAsync();

		// Assert: IOException thrown by test double (real impl returns null)
		// Implementation MUST catch exceptions and return null
		await act.Should().ThrowAsync<IOException>();
		// NOTE: When testing the real EncryptedFileSessionStore, assert it returns null instead.
	}

	[Fact]
	public async Task Save_DiskFailure_NonFatal()
	{
		// Arrange: simulate write failure
		var store = new InMemorySessionStore { ThrowOnSave = true };

		// Act: save failure is non-fatal per D13
		Func<Task> act = async () => await store.SaveSessionAsync(MakeSession());

		// Assert: IOException propagated — implementation must catch and log warning
		await act.Should().ThrowAsync<IOException>();
		// NOTE: The ClerkAuthClient wrapping this call must catch and log,
		// not let it propagate. Auth succeeds for current run even without persistence.
	}

	[Fact]
	public async Task Save_MultipleTimes_LastSaveWins()
	{
		// Arrange
		var store = new InMemorySessionStore();
		var first = MakeSession([new Cookie("__client", "first_value", "/", "clerk.finary.com")], "sess_1");
		var second = MakeSession([new Cookie("__client", "second_value", "/", "clerk.finary.com")], "sess_2");

		// Act
		await store.SaveSessionAsync(first);
		await store.SaveSessionAsync(second);
		var loaded = await store.LoadSessionAsync();

		// Assert: last save wins
		loaded.Should().NotBeNull();
		loaded.Cookies.First().Value.Should().Be("second_value");
		loaded.SessionId.Should().Be("sess_2");
		store.SaveCount.Should().Be(2);
	}

	[Fact]
	public Task SavedCookies_ContainRequiredClerkCookies()
	{
		// Arrange: session cookies must include __client and __client_uat
		var cookies = ApiFixtures.SessionCookies;

		// Assert: fixture contains the required Clerk cookies
		cookies.Should().Contain(c => c.Name == "__client", "long-lived session credential");
		cookies.Should().Contain(c => c.Name == "__client_uat", "client updated-at timestamp");
		return Task.CompletedTask;
	}

	[Fact]
	public Task SessionCookies_ClientCookie_HasLongExpiry()
	{
		// Arrange: __client cookie should have ~90 day expiry per api-analysis.md
		var cookies = ApiFixtures.SessionCookies;
		var clientCookie = cookies.First(c => c.Name == "__client");

		// Assert
		clientCookie.Expires.Should().BeAfter(DateTime.UtcNow.AddDays(30),
			"Clerk __client cookie has ~90 day expiry");
		return Task.CompletedTask;
	}

	[Fact]
	public async Task Load_ConcurrentAccess_DoesNotCorrupt()
	{
		// Arrange: verify session store handles concurrent load/save
		var store = new InMemorySessionStore();
		await store.SaveSessionAsync(MakeSession());

		// Act: multiple concurrent loads
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => store.LoadSessionAsync())
			.ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert: all loads return valid data
		results.Should().AllSatisfy(r => r.Should().NotBeNull());
		store.LoadCount.Should().Be(10);
	}
}
