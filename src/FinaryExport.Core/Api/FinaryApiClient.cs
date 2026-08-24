using System.Text.Json;
using FinaryExport.Models;
using FinaryExport.Models.User;
using Microsoft.Extensions.Logging;

using static FinaryExport.FinaryConstants;

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

	private OrganizationContext? _context;

	private HttpClient CreateClient() => httpClientFactory.CreateClient(ApiPaths.HttpClientName);

	private string BasePath
	{
		get
		{
			var context = Volatile.Read(ref _context)
				?? throw new InvalidOperationException(
					"Organization context has not been initialized. Resolve or set a profile first.");
			return $"/organizations/{context.OrgId}/memberships/{context.MembershipId}";
		}
	}

	public async Task<(string OrgId, string MembershipId)> GetOrganizationContextAsync(CancellationToken ct)
	{
		var orgs = await GetAsync<List<Organization>>(ApiPaths.UsersOrganizationsPath, ct)
			?? throw new InvalidOperationException("No organizations found");

		// Find the org where user is owner
		foreach (var org in orgs)
		{
			var ownerMember = org.Members?.FirstOrDefault(m => m.User?.IsOrganizationOwner == true);
			if (ownerMember is null)
				continue;

			var orgId = org.Id ?? throw new InvalidOperationException("Organization has no ID");
			var membershipId = ownerMember.Id ?? throw new InvalidOperationException("Membership has no ID");
			Volatile.Write(ref _context, new OrganizationContext(orgId, membershipId));

			logger.LogInformation("Organization context initialized");
			return (orgId, membershipId);
		}

		throw new InvalidOperationException("No organization found where user is owner");
	}

	public async Task<List<FinaryProfile>> GetAllProfilesAsync(CancellationToken ct)
	{
		var orgs = await GetAsync<List<Organization>>(ApiPaths.UsersOrganizationsPath, ct)
			?? throw new InvalidOperationException("No organizations found");

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
		ArgumentException.ThrowIfNullOrWhiteSpace(orgId);
		ArgumentException.ThrowIfNullOrWhiteSpace(membershipId);
		Volatile.Write(ref _context, new OrganizationContext(orgId, membershipId));
		logger.LogDebug("Organization context changed");
	}

	// Fetches and unwraps a FinaryResponse envelope.
	private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
	{
		var client = CreateClient();
		var response = await client.GetAsync(path, ct);
		var body = await response.Content.ReadAsStringAsync(ct);

		if (!response.IsSuccessStatusCode)
		{
			var errorMessage = GetApiErrorMessage(body) ?? response.ReasonPhrase ?? "Request failed";
			throw new HttpRequestException(
				$"Finary API returned {(int)response.StatusCode} ({response.StatusCode}): {errorMessage}",
				null,
				response.StatusCode);
		}

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
		if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
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

	// Fetches one logical slice from a page-based endpoint without reading earlier pages.
	private async Task<List<T>> GetPaginatedListPageAsync<T>(
		string basePath,
		int pageSize,
		int offset,
		int limit,
		CancellationToken ct)
	{
		if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
		if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));

		var page = (offset / pageSize) + 1;
		var skip = offset % pageSize;
		var result = new List<T>(limit);

		while (result.Count < limit)
		{
			var separator = basePath.Contains('?') ? "&" : "?";
			var path = $"{basePath}{separator}page={page}&per_page={pageSize}";
			var batch = await GetAsync<List<T>>(path, ct) ?? [];
			var isLastPage = batch.Count < pageSize;

			if (skip > 0)
			{
				batch = [.. batch.Skip(skip)];
				skip = 0;
			}

			result.AddRange(batch.Take(limit - result.Count));
			if (isLastPage) break;
			page++;
		}

		return result;
	}

	public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken ct)
	{
		return await GetAsync<UserProfile>(ApiPaths.CurrentUserPath, ct);
	}

	private string? GetApiErrorMessage(string body)
	{
		try
		{
			var errorEnvelope = JsonSerializer.Deserialize<FinaryResponse<JsonElement>>(body, _jsonOptions);
			return errorEnvelope?.Error?.Message ?? errorEnvelope?.Message;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private sealed record OrganizationContext(string OrgId, string MembershipId);
}
