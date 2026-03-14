using FinaryExport.Api;
using FinaryExport.Auth;
using FinaryExport.Configuration;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Infrastructure;
using Loxifi.CurlImpersonate;
using Microsoft.Extensions.DependencyInjection;

namespace FinaryExport.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinaryExport(this IServiceCollection services)
    {
        // CurlImpersonate client for Finary API calls (Chrome TLS fingerprint to bypass Cloudflare)
        services.AddSingleton(_ => new CurlClient(BrowserProfile.Chrome136));

        // Auth services (ClerkAuthClient uses CurlImpersonate directly for Clerk calls)
        services.AddSingleton<ICredentialPrompt, ConsoleCredentialPrompt>();
        services.AddSingleton<ISessionStore, EncryptedFileSessionStore>();
        services.AddSingleton<ClerkAuthClient>();
        services.AddSingleton<ITokenProvider>(sp => sp.GetRequiredService<ClerkAuthClient>());
        services.AddHostedService<TokenRefreshService>();

        // Rate limiter
        services.AddSingleton<RateLimiter>();

        // Finary API HTTP client (CurlImpersonate-backed for Cloudflare bypass)
        services.AddTransient<FinaryDelegatingHandler>();
        services.AddHttpClient("Finary", client =>
        {
            client.BaseAddress = new Uri("https://api.finary.com");
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
            new CurlMessageHandler(sp.GetRequiredService<CurlClient>()))
        .AddHttpMessageHandler<FinaryDelegatingHandler>();

        // API client
        services.AddSingleton<IFinaryApiClient, FinaryApiClient>();

        // Export services
        services.AddSingleton<IWorkbookExporter, WorkbookExporter>();
        services.AddSingleton<ISheetWriter, PortfolioSummarySheet>();
        services.AddSingleton<ISheetWriter, AccountsSheet>();
        services.AddSingleton<ISheetWriter, TransactionsSheet>();
        services.AddSingleton<ISheetWriter, DividendsSheet>();
        services.AddSingleton<ISheetWriter, HoldingsSheet>();

        return services;
    }
}
