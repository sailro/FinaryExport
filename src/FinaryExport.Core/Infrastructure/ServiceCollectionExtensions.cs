using FinaryExport.Api;
using FinaryExport.Auth;
using Loxifi.CurlImpersonate;
using Microsoft.Extensions.DependencyInjection;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Infrastructure;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFinaryCore(this IServiceCollection services)
	{
		// CurlImpersonate client for Finary API calls (Chrome TLS fingerprint to bypass Cloudflare)
		services.AddSingleton(_ => new CurlClient(ImpersonationProfile));

		// Auth services (ClerkAuthClient uses CurlImpersonate directly for Clerk calls)
		// NOTE: ICredentialPrompt is NOT registered here — host project must register its own implementation
		services.AddSingleton<ISessionStore, EncryptedFileSessionStore>();
		services.AddSingleton<ClerkAuthClient>();
		services.AddSingleton<ITokenProvider>(sp => sp.GetRequiredService<ClerkAuthClient>());
		services.AddHostedService<TokenRefreshService>();

		// Rate limiter
		services.AddSingleton<RateLimiter>();

		// Finary API HTTP client (CurlImpersonate-backed for Cloudflare bypass)
		services.AddTransient<FinaryDelegatingHandler>();
		services.AddHttpClient(ApiPaths.HttpClientName, client =>
		{
			client.BaseAddress = new Uri(ApiBaseUrl);
		})
		.ConfigurePrimaryHttpMessageHandler(sp =>
			new CurlMessageHandler(sp.GetRequiredService<CurlClient>()))
		.AddHttpMessageHandler<FinaryDelegatingHandler>();

		// API client
		services.AddSingleton<IFinaryApiClient, FinaryApiClient>();

		return services;
	}
}
