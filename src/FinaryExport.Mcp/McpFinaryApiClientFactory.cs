using FinaryExport.Api;
using FinaryExport.Models.User;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Mcp;

public interface IMcpFinaryApiClientFactory
{
	Task<IFinaryApiClient> CreateActiveClientAsync(CancellationToken ct = default);
	IFinaryApiClient CreateGlobalClient();
	Task<FinaryProfile> SetActiveProfileAsync(
		string organizationId,
		string membershipId,
		CancellationToken ct = default);
}

// Owns only the MCP session's selected profile. Every tool invocation receives a
// fresh Core client with a fixed context snapshot, so concurrent MCP calls cannot
// change one another's in-flight organization/membership context.
public sealed class McpFinaryApiClientFactory(
	IHttpClientFactory httpClientFactory,
	ILoggerFactory loggerFactory,
	ILogger<McpFinaryApiClientFactory> logger) : IMcpFinaryApiClientFactory
{
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private ActiveContext? _activeContext;

	public IFinaryApiClient CreateGlobalClient() => CreateClient();

	public async Task<IFinaryApiClient> CreateActiveClientAsync(CancellationToken ct = default)
	{
		var context = Volatile.Read(ref _activeContext) ?? await InitializeOwnerContextAsync(ct);
		return CreateClient(context);
	}

	public async Task<FinaryProfile> SetActiveProfileAsync(
		string organizationId,
		string membershipId,
		CancellationToken ct = default)
	{
		var profiles = await CreateClient().GetAllProfilesAsync(ct);
		var profile = profiles.FirstOrDefault(item =>
			string.Equals(item.OrgId, organizationId, StringComparison.Ordinal) &&
			string.Equals(item.MembershipId, membershipId, StringComparison.Ordinal));

		if (profile is null)
			throw McpErrors.InvalidInput(
				"The organization/membership pair is not present in get_profiles.");

		Volatile.Write(ref _activeContext, new ActiveContext(profile.OrgId, profile.MembershipId));
		logger.LogDebug("MCP active profile changed");
		return profile;
	}

	private async Task<ActiveContext> InitializeOwnerContextAsync(CancellationToken ct)
	{
		await _initializationLock.WaitAsync(ct);
		try
		{
			var existing = Volatile.Read(ref _activeContext);
			if (existing is not null) return existing;

			logger.LogInformation("Initializing the MCP session with the owner profile");
			var client = CreateClient();
			var (organizationId, membershipId) = await client.GetOrganizationContextAsync(ct);
			var initialized = new ActiveContext(organizationId, membershipId);
			Volatile.Write(ref _activeContext, initialized);
			return initialized;
		}
		finally
		{
			_initializationLock.Release();
		}
	}

	private FinaryApiClient CreateClient(ActiveContext? context = null)
	{
		var client = new FinaryApiClient(
			httpClientFactory,
			loggerFactory.CreateLogger<FinaryApiClient>());
		if (context is not null)
			client.SetOrganizationContext(context.OrganizationId, context.MembershipId);
		return client;
	}

	private sealed record ActiveContext(string OrganizationId, string MembershipId);
}
