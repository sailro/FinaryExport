using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class UserTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_user_profile", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get a privacy-limited authenticated user profile with identity, subscription, and display currency")]
	public async Task<UserProfileResponse> GetUserProfile(CancellationToken ct = default)
	{
		var api = clients.CreateGlobalClient();
		return McpMapper.User(await api.GetCurrentUserAsync(ct));
	}

	[McpServerTool(Name = "get_profiles", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("List profiles available for active-profile queries")]
	public async Task<ProfilesResponse> GetProfiles(CancellationToken ct = default)
	{
		var api = clients.CreateGlobalClient();
		return McpMapper.Profiles(await api.GetAllProfilesAsync(ct));
	}

	[McpServerTool(Name = "set_active_profile", UseStructuredContent = true, ReadOnly = false, Idempotent = true), Description("Switch the MCP session's active profile for subsequent read-only queries. Use get_profiles to obtain the IDs.")]
	public async Task<ActiveProfileResponse> SetActiveProfile(
		[Description("Organization ID from the profile list")] string orgId,
		[Description("Membership ID from the profile list")] string membershipId,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(orgId))
			throw McpErrors.InvalidInput("Organization ID is required.");
		if (string.IsNullOrWhiteSpace(membershipId))
			throw McpErrors.InvalidInput("Membership ID is required.");

		await clients.SetActiveProfileAsync(orgId, membershipId, ct);
		return new ActiveProfileResponse
		{
			OrganizationId = orgId,
			MembershipId = membershipId
		};
	}
}
