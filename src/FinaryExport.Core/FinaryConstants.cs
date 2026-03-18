using Loxifi.CurlImpersonate;

namespace FinaryExport;

// Shared constants for Finary API endpoints, headers, and TLS impersonation.
public static class FinaryConstants
{
	public static readonly BrowserProfile ImpersonationProfile = BrowserProfile.Chrome136;

	public const string ApiBaseUrl = "https://api.finary.com";
	public const string AppOrigin = "https://app.finary.com";
	public const string ClerkBaseUrl = "https://clerk.finary.com";

	public static class ApiPaths
	{
		public const string HttpClientName = "Finary";
		public const string UsersOrganizationsPath = "/users/me/organizations";
		public const string CurrentUserPath = "/users/me";
	}

	public static class Headers
	{
		public const string ApiVersionHeader = "x-client-api-version";
		public const string ApiVersionValue = "2";
		public const string ClientIdHeader = "x-finary-client-id";
		public const string ClientIdValue = "webapp";
	}

	public static class Defaults
	{
		public const string DefaultPeriod = "all";
		public const string DefaultValueType = "gross";
		public const int DefaultTransactionPageSize = 200;
	}
}
