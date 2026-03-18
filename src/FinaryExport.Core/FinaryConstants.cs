using Loxifi.CurlImpersonate;

namespace FinaryExport;

/// <summary>
/// Shared constants for Finary API endpoints, headers, and TLS impersonation.
/// </summary>
public static class FinaryConstants
{
	public static readonly BrowserProfile ImpersonationProfile = BrowserProfile.Chrome136;

	public const string ApiBaseUrl = "https://api.finary.com";
	public const string AppOrigin = "https://app.finary.com";
	public const string ClerkBaseUrl = "https://clerk.finary.com";
}
