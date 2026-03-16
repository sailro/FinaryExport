using System.Text.Json;
using FinaryExport.Models;
using FinaryExport.Models.User;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Api;

// Finary API client. Split into partial classes by concern.
// This file contains core setup, helpers, and org context resolution.
public sealed partial class FinaryApiClient(IHttpClientFactory httpClientFactory, ILogger<FinaryApiClient> logger)
	: IFinaryApiClient
{
	private readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		PropertyNameCaseInsensitive = true
	};

	private string? _orgId;
	private string? _membershipId;

	private HttpClient CreateClient() => httpClientFactory.CreateClient("Finary");

	private string BasePath => $"/organizations/{_orgId}/memberships/{_membershipId}";

	public async Task<(string OrgId, string MembershipId)> GetOrganizationContextAsync(CancellationToken ct)
	{
		var client = CreateClient();
		var response = await client.GetAsync("/users/me/organizations", ct);
		response.EnsureSuccessStatusCode();

		var body = await response.Content.ReadAsStringAsync(ct);
		var result = JsonSerializer.Deserialize<FinaryResponse<List<Organization>>>(body, _jsonOptions);
		var orgs = result?.Result ?? throw new InvalidOperationException("No organizations found");

		// Find the org where user is owner
		foreach (var org in orgs)
		{
			var ownerMember = org.Members?.FirstOrDefault(m => m.User?.IsOrganizationOwner == true);
			if (ownerMember is null)
				continue;

			_orgId = org.Id ?? throw new InvalidOperationException("Organization has no ID");
			_membershipId = ownerMember.Id ?? throw new InvalidOperationException("Membership has no ID");

			logger.LogInformation("Organization: {OrgName} (membership: {MembershipId})", org.Name, _membershipId);
			return (_orgId, _membershipId);
		}

		throw new InvalidOperationException("No organization found where user is owner");
	}

	public async Task<List<FinaryProfile>> GetAllProfilesAsync(CancellationToken ct)
	{
		var client = CreateClient();
		var response = await client.GetAsync("/users/me/organizations", ct);
		response.EnsureSuccessStatusCode();

		var body = await response.Content.ReadAsStringAsync(ct);
		var result = JsonSerializer.Deserialize<FinaryResponse<List<Organization>>>(body, _jsonOptions);
		var orgs = result?.Result ?? throw new InvalidOperationException("No organizations found");

		var profiles = new List<FinaryProfile>();

		foreach (var org in orgs)
		{
			var orgId = org.Id ?? throw new InvalidOperationException("Organization has no ID");

			foreach (var member in org.Members ?? [])
			{
				var membershipId = member.Id ?? throw new InvalidOperationException("Membership has no ID");
				var name = member.User?.Fullname ?? org.Name ?? membershipId;
				profiles.Add(new FinaryProfile(orgId, membershipId, name));
			}
		}

		if (profiles.Count == 0)
			throw new InvalidOperationException("No profiles found in organizations");

		logger.LogInformation("Found {Count} profile(s)", profiles.Count);
		return profiles;
	}

	public void SetOrganizationContext(string orgId, string membershipId)
	{
		_orgId = orgId;
		_membershipId = membershipId;
		logger.LogInformation("Switched to org {OrgId}, membership {MembershipId}", orgId, membershipId);
	}

	// Fetches and unwraps a FinaryResponse envelope.
	private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
	{
		var client = CreateClient();
		var response = await client.GetAsync(path, ct);
		response.EnsureSuccessStatusCode();

		var body = await response.Content.ReadAsStringAsync(ct);
		var envelope = JsonSerializer.Deserialize<FinaryResponse<T>>(body, _jsonOptions);

		if (envelope?.Error is not null)
		{
			logger.LogWarning("API error on {Path}: {Code} — {Message}",
				path, envelope.Error.Code, envelope.Error.Message);
		}

		return envelope is not null ? envelope.Result : default;
	}

	// Fetches a list endpoint with auto-pagination.
	private async Task<List<T>> GetPaginatedListAsync<T>(string basePath, int pageSize, CancellationToken ct)
	{
		var all = new List<T>();
		var page = 1;

		while (true)
		{
			var separator = basePath.Contains('?') ? "&" : "?";
			var path = $"{basePath}{separator}page={page}&per_page={pageSize}";
			var batch = await GetAsync<List<T>>(path, ct) ?? [];
			all.AddRange(batch);

			if (batch.Count < pageSize) break;
			page++;
		}

		return all;
	}

	public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken ct)
	{
		return await GetAsync<UserProfile>("/users/me", ct);
	}
}
