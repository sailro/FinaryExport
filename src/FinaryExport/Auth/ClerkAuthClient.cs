using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using Loxifi.CurlImpersonate;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Auth;

// Clerk authentication using curl-impersonate for TLS fingerprint bypass on clerk.finary.com.
// Cold start: sign_in → 2FA → extract session+JWT. Warm start/refresh: POST /tokens.
public sealed class ClerkAuthClient(
	ISessionStore sessionStore,
	ICredentialPrompt credentialPrompt,
	ILogger<ClerkAuthClient> logger)
	: ITokenProvider, IDisposable
{
	private const string ClerkBase = "https://clerk.finary.com";

	private volatile string _currentJwt = "";
	private readonly List<Cookie> _activeCookies = [];
	private readonly SemaphoreSlim _authLock = new(1, 1);

	public string SessionId { get; private set; } = "";

	public async Task<string> GetTokenAsync(CancellationToken ct = default)
	{
		var token = _currentJwt;
		if (!string.IsNullOrEmpty(token))
			return token;

		await _authLock.WaitAsync(ct);
		try
		{
			if (!string.IsNullOrEmpty(_currentJwt))
				return _currentJwt;

			await LoginAsync(ct);
			return _currentJwt;
		}
		finally
		{
			_authLock.Release();
		}
	}

	public async Task LoginAsync(CancellationToken ct)
	{
		if (await TryWarmStartAsync(ct))
			return;

		await ColdStartAsync(ct);
	}

	// Creates a CurlClient configured for Clerk requests (Chrome TLS fingerprint + required headers).
	private static CurlClient CreateClerkClient()
	{
		var client = new CurlClient(BrowserProfile.Chrome136) { UseCookies = true };
		client.DefaultRequestHeaders["Origin"] = "https://app.finary.com";
		client.DefaultRequestHeaders["Referer"] = "https://app.finary.com";
		client.DefaultRequestHeaders["Accept-Encoding"] = "identity";
		client.DefaultRequestHeaders["Accept"] = "*/*";
		return client;
	}

	// Populates a CurlClient's cookie container from persisted cookies.
	private static void LoadCookies(CurlClient client, IEnumerable<Cookie> cookies)
	{
		var uri = new Uri(ClerkBase);
		foreach (var cookie in cookies)
			client.CookieContainer.Add(uri, cookie);
	}

	// Warm start: use persisted session + cookies to refresh JWT.
	private async Task<bool> TryWarmStartAsync(CancellationToken ct)
	{
		logger.LogDebug("Attempting warm start...");

		var session = await sessionStore.LoadSessionAsync(ct);
		if (session is null)
		{
			logger.LogDebug("No persisted session found");
			return false;
		}

		if (session.Cookies.Count == 0)
		{
			logger.LogDebug("No cookies in persisted session");
			return false;
		}

		try
		{
			using var client = CreateClerkClient();
			LoadCookies(client, session.Cookies);

			var jwt = await PostTokensAsync(client, session.SessionId, ct);

			SessionId = session.SessionId;
			CaptureCookies(client);
			Interlocked.Exchange(ref _currentJwt, jwt);
			logger.LogInformation("Resumed session: {SessionId}", TruncateId(session.SessionId));

			await PersistSessionAsync(ct);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogDebug(ex, "Warm start failed");
			await sessionStore.ClearSessionAsync(ct);
			return false;
		}
	}

	// Cold start: prompt credentials → sign_in → 2FA → extract session + JWT.
	private async Task ColdStartAsync(CancellationToken ct)
	{
		logger.LogDebug("Starting cold authentication...");

		var (email, password, totpCode) = credentialPrompt.PromptCredentials();

		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(totpCode))
			throw new AuthenticationException("Email, password, and TOTP code are all required.");

		using var client = CreateClerkClient();

		// Step 1: POST sign_ins
		var signInResponse = await client.PostAsync(
			$"{ClerkBase}/v1/client/sign_ins",
			new FormUrlEncodedContent([
				new("identifier", email),
				new("password", password)
			]), ct);

		var signInBody = await signInResponse.Content.ReadAsStringAsync(ct);
		if (!signInResponse.IsSuccessStatusCode)
			throw new AuthenticationException($"Sign-in failed ({(int)signInResponse.StatusCode}): {signInBody}");

		using var signInDoc = JsonDocument.Parse(signInBody);
		var signInRoot = signInDoc.RootElement;

		var signInId = TryGetString(signInRoot, "response", "id")
			?? TryGetString(signInRoot, "id")
			?? throw new AuthenticationException("No sign_in ID in response");

		var status = TryGetString(signInRoot, "response", "status")
			?? TryGetString(signInRoot, "status")
			?? "";

		// Clerk sometimes requires a short wait before 2FA
		if (status == "needs_second_factor")
			await Task.Delay(500, ct);

		// Step 2: POST attempt_second_factor
		var totpResponse = await client.PostAsync(
			$"{ClerkBase}/v1/client/sign_ins/{signInId}/attempt_second_factor",
			new FormUrlEncodedContent([
				new("strategy", "totp"),
				new("code", totpCode)
			]), ct);

		var totpBody = await totpResponse.Content.ReadAsStringAsync(ct);
		if (!totpResponse.IsSuccessStatusCode)
			throw new AuthenticationException($"2FA failed ({(int)totpResponse.StatusCode}): {totpBody}");

		using var totpDoc = JsonDocument.Parse(totpBody);
		var totpRoot = totpDoc.RootElement;

		// Extract session ID and JWT from 2FA response
		string? sessionId = null;
		string? jwt = null;

		// Try nested client.sessions[0] structure (full Clerk response envelope)
		if (totpRoot.TryGetProperty("client", out var clientEl) &&
			clientEl.TryGetProperty("sessions", out var sessions) &&
			sessions.ValueKind == JsonValueKind.Array && sessions.GetArrayLength() > 0)
		{
			var firstSession = sessions[0];
			sessionId = firstSession.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

			if (firstSession.TryGetProperty("last_active_token", out var tokenEl) &&
				tokenEl.TryGetProperty("jwt", out var jwtEl))
				jwt = jwtEl.GetString();
		}

		// Fallback: created_session_id at top level or under response
		sessionId ??= TryGetString(totpRoot, "response", "created_session_id")
			?? TryGetString(totpRoot, "created_session_id")
			?? throw new AuthenticationException("No session ID in 2FA response");

		// If JWT wasn't in the 2FA response, fetch via /tokens
		jwt ??= await PostTokensAsync(client, sessionId, ct);

		SessionId = sessionId;
		CaptureCookies(client);
		Interlocked.Exchange(ref _currentJwt, jwt);
		logger.LogInformation("New session: {SessionId}", TruncateId(sessionId));

		await PersistSessionAsync(ct);
	}

	// Refreshes the JWT token. Called by TokenRefreshService every 50s.
	public async Task RefreshTokenAsync(CancellationToken ct)
	{
		if (string.IsNullOrEmpty(SessionId))
			throw new InvalidOperationException("Cannot refresh token before login");

		using var client = CreateClerkClient();
		LoadCookies(client, _activeCookies);

		try
		{
			var jwt = await PostTokensAsync(client, SessionId, ct);
			CaptureCookies(client);
			Interlocked.Exchange(ref _currentJwt, jwt);
			logger.LogDebug("Token refreshed");
		}
		catch (AuthenticationException)
		{
			throw;
		}
		catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
		{
			throw new AuthenticationException("Session expired, re-authentication required");
		}
	}

	// POST /v1/client/sessions/{sessionId}/tokens → returns JWT string.
	private static async Task<string> PostTokensAsync(CurlClient client, string sessionId, CancellationToken ct)
	{
		var response = await client.PostAsync(
			$"{ClerkBase}/v1/client/sessions/{sessionId}/tokens",
			new FormUrlEncodedContent([]), ct);

		if (response.StatusCode == HttpStatusCode.Unauthorized)
			throw new AuthenticationException("Session expired during token refresh");

		var body = await response.Content.ReadAsStringAsync(ct);
		response.EnsureSuccessStatusCode();

		using var doc = JsonDocument.Parse(body);
		return doc.RootElement.GetProperty("jwt").GetString()
			?? throw new AuthenticationException("Token response missing JWT");
	}

	// Extracts cookies from CurlClient's container for persistence.
	private void CaptureCookies(CurlClient client)
	{
		_activeCookies.Clear();
		foreach (Cookie cookie in client.CookieContainer.GetCookies(new Uri(ClerkBase)))
			_activeCookies.Add(cookie);
	}

	private async Task PersistSessionAsync(CancellationToken ct)
	{
		try
		{
			await sessionStore.SaveSessionAsync(
				new SessionData(SessionId, _activeCookies.AsReadOnly()), ct);
			logger.LogDebug("Persisted session ({Count} cookies)", _activeCookies.Count);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to persist session");
		}
	}

	// Navigates a JSON object path and returns the string value, or null if not found.
	private static string? TryGetString(JsonElement el, params string[] path)
	{
		var current = el;
		foreach (var prop in path)
		{
			if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(prop, out current))
				return null;
		}
		return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
	}

	private static string TruncateId(string id) =>
		id.Length > 12 ? $"{id[..12]}..." : id;

	public void Dispose()
	{
		_authLock.Dispose();
	}
}
