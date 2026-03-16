using FinaryExport.Auth;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp;

// Prompts the user for Finary credentials via MCP Elicitation when no session.dat exists.
// Requires the MCP client (host) to advertise elicitation capability during initialization.
public sealed class McpCredentialPrompt(McpServer mcpServer) : ICredentialPrompt
{
	public async Task<(string Email, string Password, string TotpCode)> PromptCredentialsAsync(CancellationToken ct = default)
	{
		EnsureElicitationSupported();

		var schema = new ElicitRequestParams.RequestSchema
		{
			Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
			{
				["email"] = new ElicitRequestParams.StringSchema
				{
					Title = "Email",
					Description = "Your Finary account email address",
					Format = "email"
				},
				["password"] = new ElicitRequestParams.StringSchema
				{
					Title = "Password",
					Description = "Your Finary account password",
					Format = "password"
				},
				["totp_code"] = new ElicitRequestParams.StringSchema
				{
					Title = "TOTP Code",
					Description = "The 6-digit code from your authenticator app",
					MinLength = 6,
					MaxLength = 6
				}
			},
			Required = ["email", "password", "totp_code"]
		};

		var requestParams = new ElicitRequestParams
		{
			Message = "Finary authentication required. Please enter your credentials.",
			RequestedSchema = schema
		};

		try
		{
			var result = await mcpServer.ElicitAsync(requestParams, ct);

			if (!result.IsAccepted || result.Content is null)
				throw new InvalidOperationException("Authentication cancelled — credentials are required to connect to Finary.");

			var email = result.Content["email"].GetString()
				?? throw new InvalidOperationException("Email is required.");
			var password = result.Content["password"].GetString()
				?? throw new InvalidOperationException("Password is required.");
			var totpCode = result.Content["totp_code"].GetString()
				?? throw new InvalidOperationException("TOTP code is required.");

			return (email, password, totpCode);
		}
		catch (InvalidOperationException) when (!IsElicitationSupported())
		{
			// The SDK threw because client capabilities changed or weren't fully negotiated.
			// Re-throw with the same guidance as the pre-flight check.
			throw new InvalidOperationException(NoElicitationMessage());
		}
		catch (Exception ex) when (ex is not InvalidOperationException)
		{
			throw new InvalidOperationException(
				"Finary authentication required but credential prompting failed. " +
				$"Details: {ex.Message}\n\n" +
				"Workaround: run the FinaryExport CLI first to create a session (session.dat), " +
				"then the MCP server will reuse it without needing elicitation.", ex);
		}
	}

	// Pre-flight: verify elicitation capability and backfill the form sub-mode if needed.
	// Some clients (e.g. Copilot CLI) send elicitation: {} without the form sub-capability
	// introduced in the 2025-06-18 spec revision. The SDK 1.1.0 requires Form to be non-null
	// for form-mode requests, so we backfill it when the client supports elicitation in general.
	private void EnsureElicitationSupported()
	{
		var caps = mcpServer.ClientCapabilities;
		if (caps?.Elicitation is null)
			throw new InvalidOperationException(NoElicitationMessage());

		caps.Elicitation.Form ??= new FormElicitationCapability();
	}

	private bool IsElicitationSupported() =>
		mcpServer.ClientCapabilities?.Elicitation is not null;

	private static string NoElicitationMessage() =>
		"Finary authentication required but no session.dat exists, " +
		"and the MCP client (host) does not support elicitation. " +
		"The server needs to prompt for email, password, and TOTP code, " +
		"but the client did not advertise 'elicitation' capability during initialization.\n\n" +
		"Workaround: run the FinaryExport CLI first to create a session (session.dat), " +
		"then the MCP server will reuse it without needing elicitation.";
}
