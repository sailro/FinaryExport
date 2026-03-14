namespace FinaryExport.Auth;

// Provides user credentials for cold-start authentication.
// Decoupled from ClerkAuthClient to allow testing and alternative implementations.
public interface ICredentialPrompt
{
	(string Email, string Password, string TotpCode) PromptCredentials();
}
