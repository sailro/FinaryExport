namespace FinaryExport.Auth;

// Provides a valid JWT for Finary API calls.
// Implementation handles Clerk authentication and token refresh internally.
public interface ITokenProvider
{
	// Returns a valid JWT. Blocks briefly if a refresh is in-flight.
	Task<string> GetTokenAsync(CancellationToken ct = default);

	// Session ID established during login.
	string SessionId { get; }
}
