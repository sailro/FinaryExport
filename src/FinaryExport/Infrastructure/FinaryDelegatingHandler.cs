using FinaryExport.Api;
using FinaryExport.Auth;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Infrastructure;

// Adds required headers to every Finary API request:
// Authorization, Origin, Referer, x-client-api-version, x-finary-client-id, Accept.
// Also enforces rate limiting.
public sealed class FinaryDelegatingHandler(
	ITokenProvider tokenProvider,
	RateLimiter rateLimiter,
	ILogger<FinaryDelegatingHandler> logger)
	: DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request, CancellationToken cancellationToken)
	{
		await rateLimiter.WaitAsync(cancellationToken);

		var token = await tokenProvider.GetTokenAsync(cancellationToken);
		request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
		request.Headers.TryAddWithoutValidation("Origin", "https://app.finary.com");
		request.Headers.TryAddWithoutValidation("Referer", "https://app.finary.com/");
		request.Headers.TryAddWithoutValidation("x-client-api-version", "2");
		request.Headers.TryAddWithoutValidation("x-finary-client-id", "webapp");
		request.Headers.TryAddWithoutValidation("Accept", "*/*");

		var response = await base.SendAsync(request, cancellationToken);

		// Log non-success responses for debugging
		if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotModified)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			logger.LogDebug("API {Status} on {Url}: {Body}",
				(int)response.StatusCode, request.RequestUri, body[..Math.Min(500, body.Length)]);
		}

		// Handle 401: force token refresh and retry once
		if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
		{
			token = await tokenProvider.GetTokenAsync(cancellationToken);
			var retry = CloneRequest(request, token);
			response.Dispose();
			response = await base.SendAsync(retry, cancellationToken);
		}

		// Handle 429: backoff and retry up to 3 times
		for (var attempt = 0; attempt < 3 && response.StatusCode == (System.Net.HttpStatusCode)429; attempt++)
		{
			var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
			await Task.Delay(retryAfter, cancellationToken);
			response.Dispose();
			var retry = CloneRequest(request, token);
			response = await base.SendAsync(retry, cancellationToken);
		}

		return response;
	}

	private static HttpRequestMessage CloneRequest(HttpRequestMessage original, string token)
	{
		var clone = new HttpRequestMessage(original.Method, original.RequestUri);
		clone.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
		clone.Headers.TryAddWithoutValidation("Origin", "https://app.finary.com");
		clone.Headers.TryAddWithoutValidation("Referer", "https://app.finary.com/");
		clone.Headers.TryAddWithoutValidation("x-client-api-version", "2");
		clone.Headers.TryAddWithoutValidation("x-finary-client-id", "webapp");

		if (original.Content is not null)
			clone.Content = original.Content;

		return clone;
	}
}
