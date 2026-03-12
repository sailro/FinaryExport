using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using FinaryExport.Configuration;
using FinaryExport.Models.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinaryExport.Auth;

// Clerk authentication with Cloudflare-aware request fingerprinting.
// Owns its HttpClient + CookieContainer for full cookie jar control.
// Flow: warmup → client init (/environment + /client for __client cookie) → sign_in → 2FA → extract session.
public sealed class ClerkAuthClient : ITokenProvider, IDisposable
{
    private readonly ISessionStore _sessionStore;
    private readonly ICredentialPrompt _credentialPrompt;
    private readonly FinaryOptions _options;
    private readonly ILogger<ClerkAuthClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private readonly CookieContainer _cookieContainer;
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _httpClient;

    private const string ClerkRoot = "https://clerk.finary.com";
    private const string AppRoot = "https://app.finary.com";
    private const string ClerkApiVersion = "2025-11-10";
    private const string ClerkJsVersion = "5.125.4";

    // Chrome-like UA to pass Cloudflare bot detection
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";

    private const int MaxRetries = 3;
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30)
    ];

    private volatile string _currentJwt = "";
    private string _sessionId = "";
    private readonly SemaphoreSlim _authLock = new(1, 1);

    public string SessionId => _sessionId;

    public ClerkAuthClient(
        ISessionStore sessionStore,
        ICredentialPrompt credentialPrompt,
        IOptions<FinaryOptions> options,
        ILogger<ClerkAuthClient> logger)
    {
        _sessionStore = sessionStore;
        _credentialPrompt = credentialPrompt;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        _cookieContainer = new CookieContainer();
        _handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        _httpClient = new HttpClient(_handler);
    }

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
        if (_options.ClearSession)
        {
            _logger.LogInformation("Clear session requested, forcing cold start");
            await _sessionStore.ClearSessionAsync(ct);
        }

        // Try warm start first
        if (!_options.ClearSession && await TryWarmStartAsync(ct))
            return;

        // Fall back to cold start
        await ColdStartAsync(ct);
    }

    // Warm start: restore cookies + sessionId from store, call /tokens to get a fresh JWT.
    private async Task<bool> TryWarmStartAsync(CancellationToken ct)
    {
        _logger.LogDebug("Attempting warm start...");

        var session = await _sessionStore.LoadSessionAsync(ct);
        if (session is null)
        {
            _logger.LogDebug("No persisted session found");
            return false;
        }

        // Restore all cookies (Clerk + Cloudflare) to the container
        foreach (var cookie in session.Cookies)
        {
            var domain = cookie.Domain.TrimStart('.');
            _cookieContainer.Add(new Uri($"https://{domain}"), cookie);
        }

        try
        {
            var jwt = await RequestTokenAsync(session.SessionId, ct);
            _sessionId = session.SessionId;
            Interlocked.Exchange(ref _currentJwt, jwt);
            _logger.LogInformation("Resumed session: {SessionId}", TruncateId(session.SessionId));
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogDebug("Warm start got 401, clearing session");
            await _sessionStore.ClearSessionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Warm start failed");
            await _sessionStore.ClearSessionAsync(ct);
        }

        return false;
    }

    // Cold start: interactive prompts → Cloudflare warmup → Clerk client init → sign_in → 2FA → save session.
    // Calls /v1/client first to obtain the __client cookie (required by Clerk bot detection).
    private async Task ColdStartAsync(CancellationToken ct)
    {
        _logger.LogDebug("Starting cold authentication...");

        var (email, password, totpCode) = _credentialPrompt.PromptCredentials();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(totpCode))
            throw new AuthenticationException("Email, password, and TOTP code are all required.");

        // Step 0: Cloudflare warmup — pick up __cf_bm and _cfuvid cookies
        await CloudflareWarmupAsync(ct);

        // Step 0.5: Clerk client init — GET /v1/client sets the __client cookie.
        // Without this cookie, Clerk flags POST /sign_ins as bot traffic (403 bot_detected).
        await ClerkClientInitAsync(ct);

        // Step 1: Sign in with email + password
        _logger.LogDebug("Step 1/3: Signing in...");
        var result = await PostClerkAsync<ClerkApiResponse>(
            "/v1/client/sign_ins",
            new Dictionary<string, string>
            {
                ["identifier"] = email,
                ["password"] = password,
                ["locale"] = "fr-FR"
            }, ct);

        // Step 2: 2FA if required
        if (result?.Response?.Status == "needs_second_factor")
        {
            var signInId = result.Response.Id
                ?? throw new AuthenticationException("Sign-in response missing ID for 2FA");

            _logger.LogDebug("Step 2/3: Submitting TOTP...");
            result = await PostClerkAsync<ClerkApiResponse>(
                $"/v1/client/sign_ins/{signInId}/attempt_second_factor",
                new Dictionary<string, string>
                {
                    ["strategy"] = "totp",
                    ["code"] = totpCode
                }, ct);
        }

        if (result?.Response?.Status != "complete")
            throw new AuthenticationException($"Sign-in failed. Status: {result?.Response?.Status}");

        // Step 3: Extract session from response (client.sessions[0])
        _logger.LogDebug("Step 3/3: Extracting session...");
        var session = result.Client?.Sessions?.FirstOrDefault()
            ?? throw new AuthenticationException("No session in sign-in response");

        var sessionId = session.Id
            ?? throw new AuthenticationException("Session ID is null");
        var jwt = session.LastActiveToken?.Jwt
            ?? throw new AuthenticationException("No JWT in session response");

        _sessionId = sessionId;
        Interlocked.Exchange(ref _currentJwt, jwt);
        _logger.LogInformation("New session: {SessionId}", TruncateId(sessionId));

        // Persist sessionId + all cookies for warm start next time
        await PersistSessionAsync(ct);
    }

    // GET app.finary.com to pick up Cloudflare cookies (__cf_bm, _cfuvid).
    // Cookies auto-flow via CookieContainer to clerk.finary.com on subsequent requests.
    private async Task CloudflareWarmupAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Cloudflare warmup...");
            using var request = new HttpRequestMessage(HttpMethod.Get, AppRoot);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "fr-FR,fr;q=0.9");

            using var response = await _httpClient.SendAsync(request, ct);
            _logger.LogDebug("Warmup: HTTP {Status}, {Count} cookies in jar",
                (int)response.StatusCode, _cookieContainer.Count);
        }
        catch (Exception ex)
        {
            // Warmup failure is non-fatal — auth may still succeed without CF cookies
            _logger.LogWarning(ex, "Cloudflare warmup failed (non-fatal)");
        }
    }

    // Clerk client init: GET /v1/environment + /v1/client to obtain the __client cookie.
    // The __client cookie is a signed JWT with a rotating token that Clerk checks on every
    // POST request. Without it, Clerk returns 403 "bot_detected".
    private async Task ClerkClientInitAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Clerk client init...");

            // GET /v1/environment (mirrors the browser's Clerk JS SDK init sequence)
            var envUrl = BuildClerkUrl("/v1/environment");
            using var envReq = new HttpRequestMessage(HttpMethod.Get, envUrl);
            ApplyBrowserHeaders(envReq);
            using var envResp = await _httpClient.SendAsync(envReq, ct);
            _logger.LogDebug("Environment: HTTP {Status}", (int)envResp.StatusCode);

            // GET /v1/client — this sets the __client, __client_uat cookies
            var clientUrl = BuildClerkUrl("/v1/client");
            using var clientReq = new HttpRequestMessage(HttpMethod.Get, clientUrl);
            ApplyBrowserHeaders(clientReq);
            using var clientResp = await _httpClient.SendAsync(clientReq, ct);
            _logger.LogDebug("Client init: HTTP {Status}, {Count} cookies in jar",
                (int)clientResp.StatusCode, _cookieContainer.Count);
        }
        catch (Exception ex)
        {
            // Non-fatal: auth might still work without the __client cookie in some cases
            _logger.LogWarning(ex, "Clerk client init failed (non-fatal)");
        }
    }

    // Refreshes the JWT token. Called by TokenRefreshService every 50s.
    public async Task RefreshTokenAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_sessionId))
            throw new InvalidOperationException("Cannot refresh token before login");

        var jwt = await RequestTokenAsync(_sessionId, ct);
        Interlocked.Exchange(ref _currentJwt, jwt);
        _logger.LogDebug("Token refreshed");
    }

    // POST /v1/client/sessions/{sessionId}/tokens → JWT
    private async Task<string> RequestTokenAsync(string sessionId, CancellationToken ct)
    {
        var result = await PostClerkAsync<ClerkTokenResponse>(
            $"/v1/client/sessions/{sessionId}/tokens",
            null, ct);

        return result?.Jwt
            ?? throw new AuthenticationException("Token response missing JWT");
    }

    // POST to Clerk API with browser headers and form-encoded body.
    private async Task<T?> PostClerkAsync<T>(
        string path,
        Dictionary<string, string>? formData,
        CancellationToken ct)
    {
        var response = await SendWithRetryAsync(() =>
        {
            var url = BuildClerkUrl(path);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            ApplyBrowserHeaders(request);

            if (formData is not null)
                request.Content = new FormUrlEncodedContent(formData);

            return request;
        }, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new AuthenticationException(
                $"Clerk error (HTTP {(int)response.StatusCode}) on {path}: {errorBody}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body, _jsonOptions);
    }

    private static string BuildClerkUrl(string path) =>
        $"{ClerkRoot}{path}?__clerk_api_version={ClerkApiVersion}&_clerk_js_version={ClerkJsVersion}";

    // Browser-like headers to defeat Cloudflare bot detection
    private static void ApplyBrowserHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Origin", AppRoot);
        request.Headers.TryAddWithoutValidation("Referer", $"{AppRoot}/");

        // Client hints (Cloudflare validates these against TLS fingerprint)
        request.Headers.TryAddWithoutValidation("sec-ch-ua",
            "\"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"Google Chrome\";v=\"146\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");

        // Fetch metadata (browsers send these on CORS requests)
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-site");

        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "fr-FR,fr;q=0.9");
    }

    private async Task PersistSessionAsync(CancellationToken ct)
    {
        try
        {
            var allCookies = _cookieContainer.GetAllCookies();
            var cookieList = allCookies.ToList();

            if (cookieList.Count > 0 && !string.IsNullOrEmpty(_sessionId))
            {
                await _sessionStore.SaveSessionAsync(new SessionData(_sessionId, cookieList), ct);
                _logger.LogDebug("Persisted session ({Count} cookies)", cookieList.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist session");
        }
    }

    // Sends request with retry on 429. Recreates request from factory on each attempt.
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var request = requestFactory();
            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            if (attempt >= MaxRetries)
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"429 after {MaxRetries} retries on {request.RequestUri}. " +
                    "Cloudflare may be blocking the request.");
            }

            var delay = GetRetryDelay(response, attempt);
            _logger.LogWarning(
                "429 on {Url}, retry {Attempt}/{Max} after {Delay:F1}s",
                request.RequestUri, attempt + 1, MaxRetries, delay.TotalSeconds);

            response.Dispose();
            await Task.Delay(delay, ct);
        }
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var retryAfter = values.FirstOrDefault();
            if (retryAfter is not null)
            {
                if (int.TryParse(retryAfter, out var seconds))
                    return TimeSpan.FromSeconds(seconds);

                if (DateTimeOffset.TryParse(retryAfter, out var date))
                {
                    var delta = date - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero)
                        return delta;
                }
            }
        }

        return BackoffDelays[attempt];
    }

    private static string TruncateId(string id) =>
        id.Length > 12 ? $"{id[..12]}..." : id;

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _authLock.Dispose();
    }
}
