using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models.User;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class UserTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_user_profile"), Description("Get the authenticated user's profile including name, email, subscription level, and display currency")]
	public async Task<UserProfile?> GetUserProfile(CancellationToken ct = default)
	{
		return await api.GetCurrentUserAsync(ct);
	}

	[McpServerTool(Name = "get_profiles"), Description("List all available profiles (memberships) the user has access to, including personal and organization profiles")]
	public async Task<List<FinaryProfile>> GetProfiles(CancellationToken ct = default)
	{
		return await api.GetAllProfilesAsync(ct);
	}

	[McpServerTool(Name = "set_active_profile"), Description("Switch the active profile for subsequent queries. Use get_profiles first to discover available org_id and membership_id values. Required when the user has multiple memberships.")]
	public string SetActiveProfile(
		[Description("Organization ID from the profile list")] string orgId,
		[Description("Membership ID from the profile list")] string membershipId)
	{
		api.SetOrganizationContext(orgId, membershipId);
		return $"Active profile switched to org={orgId}, membership={membershipId}";
	}
}
