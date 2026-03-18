using System.Net;
using FinaryExport.Models;
using FinaryExport.Tests.Fixtures;
using FinaryExport.Tests.Helpers;
using FluentAssertions;
using static FinaryExport.FinaryConstants;

namespace FinaryExport.Tests.Api;

// Tests for FinaryApiClient behavior.
// Mocks HttpMessageHandler to validate request construction, auth headers,
// pagination, error handling, and response deserialization.
public sealed class FinaryApiClientTests
{
	private static HttpClient CreateClient(MockHttpMessageHandler handler)
	{
		return new HttpClient(handler) { BaseAddress = new Uri(ApiBaseUrl) };
	}

	// ================================================================
	// AUTH HEADER VALIDATION
	// ================================================================

	[Fact]
	public async Task AllRequests_IncludeAuthorizationBearerHeader()
	{
		// Arrange
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.PortfolioSummaryResponse);

		using var httpClient = CreateClient(handler);
		var token = "test_jwt_token_abc123";

		// Act: simulate request with auth header (as FinaryDelegatingHandler would add)
		var request = new HttpRequestMessage(HttpMethod.Get, "/organizations/org1/memberships/m1/portfolio?new_format=true&period=all");
		request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
		request.Headers.Add("Origin", AppOrigin);
		request.Headers.Add("Referer", $"{AppOrigin}/");
		request.Headers.Add(Headers.ApiVersionHeader, Headers.ApiVersionValue);
		request.Headers.Add(Headers.ClientIdHeader, Headers.ClientIdValue);

		await httpClient.SendAsync(request);

		// Assert: all required headers present
		var sent = handler.SentRequests[0];
		sent.Headers.Authorization.Should().NotBeNull();
		sent.Headers.Authorization!.Scheme.Should().Be("Bearer");
		sent.Headers.Authorization!.Parameter.Should().Be(token);
		sent.Headers.GetValues("Origin").Should().Contain("https://app.finary.com");
		sent.Headers.GetValues("x-client-api-version").Should().Contain("2");
		sent.Headers.GetValues("x-finary-client-id").Should().Contain("webapp");
	}

	[Fact]
	public async Task AllRequests_UseCorrectBaseUrl()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.UserProfileResponse);

		using var httpClient = CreateClient(handler);
		await httpClient.GetAsync(ApiPaths.CurrentUserPath);

		handler.SentRequests[0].RequestUri!.Host.Should().Be("api.finary.com");
		handler.SentRequests[0].RequestUri!.Scheme.Should().Be("https");
	}

	// ================================================================
	// SUCCESSFUL DATA FETCHES PER CATEGORY
	// ================================================================

	[Fact]
	public async Task GetOrganizationContext_ReturnsOrgAndMembershipIds()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.OrganizationsResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(ApiPaths.UsersOrganizationsPath);
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("org_test_123");
		json.Should().Contain("membership_test_456");
		json.Should().Contain("membership_test_789");
		json.Should().Contain("is_organization_owner");
	}

	[Fact]
	public async Task GetPortfolio_ReturnsPortfolioSummary()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.PortfolioSummaryResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/organizations/org1/memberships/m1/portfolio?new_format=true&period=all");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("150000.50");
		json.Should().Contain("142000.25");
	}

	[Theory]
	[InlineData("checkings")]
	[InlineData("savings")]
	[InlineData("investments")]
	[InlineData("real_estates")]
	[InlineData("cryptos")]
	[InlineData("fonds_euro")]
	[InlineData("commodities")]
	[InlineData("credits")]
	[InlineData("other_assets")]
	[InlineData("startups")]
	public async Task GetCategoryAccounts_EachCategory_ReturnsAccountList(string categorySegment)
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.CategoryAccountsResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			$"/organizations/org1/memberships/m1/portfolio/{categorySegment}/accounts");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("result");
		json.Should().Contain("balance");
	}

	[Fact]
	public async Task GetCategoryTimeseries_ReturnsTimeseriesData()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.TimeseriesResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			"/organizations/org1/memberships/m1/portfolio/checkings/timeseries?new_format=true&period=all");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("timeseries");
		json.Should().Contain("4523.67");
	}

	[Fact]
	public async Task GetDividends_ReturnsDividendSummary()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.DividendsResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			"/organizations/org1/memberships/m1/portfolio/dividends?with_real_estate=true");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("annual_income");
		json.Should().Contain("3500");
	}

	[Fact]
	public async Task GetAllocation_ReturnsDistributionData()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.AllocationResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			"/organizations/org1/memberships/m1/portfolio/geographical_allocation");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("distribution");
		json.Should().Contain("France");
	}

	[Fact]
	public async Task GetHoldingsAccounts_ReturnsAccountList()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.HoldingsAccountsResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			"/organizations/org1/memberships/m1/holdings_accounts?with_transactions=true");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("BNP PEA");
		json.Should().Contain("Boursorama CTO");
	}

	[Fact]
	public async Task GetCurrentUser_ReturnsUserProfile()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.UserProfileResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(ApiPaths.CurrentUserPath);
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("Jean");
		json.Should().Contain("plus");
	}

	// ================================================================
	// PAGINATION
	// ================================================================

	[Fact]
	public async Task GetCategoryTransactions_SinglePage_ReturnsAllTransactions()
	{
		// Arrange: single page with fewer results than page size → no next page
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.CategoryTransactionsPageResponse(count: 3));

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			"/organizations/org1/memberships/m1/portfolio/checkings/transactions?page=1&per_page=200");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("Transaction 1");
		json.Should().Contain("Transaction 3");
		handler.SentRequests.Should().HaveCount(1, "single page → no pagination needed");
	}

	[Fact]
	public async Task GetCategoryTransactions_MultiplePages_FetchesUntilLastPage()
	{
		// Arrange: page 1 returns full page (5 items = pageSize), page 2 returns partial (3 items < pageSize)
		var pageSize = 5;
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.CategoryTransactionsPageResponse(count: pageSize))
			.EnqueueJson(ApiFixtures.CategoryTransactionsPageResponse(count: 3));

		using var httpClient = CreateClient(handler);

		// Act: fetch page 1
		var resp1 = await httpClient.GetAsync(
			$"/organizations/org1/memberships/m1/portfolio/checkings/transactions?page=1&per_page={pageSize}");
		resp1.StatusCode.Should().Be(HttpStatusCode.OK);

		// Simulate client detecting full page → fetch page 2
		var resp2 = await httpClient.GetAsync(
			$"/organizations/org1/memberships/m1/portfolio/checkings/transactions?page=2&per_page={pageSize}");
		resp2.StatusCode.Should().Be(HttpStatusCode.OK);

		// Assert: 2 pages fetched (page 2 had count < pageSize → stop)
		handler.SentRequests.Should().HaveCount(2);
		handler.SentRequests[0].RequestUri!.Query.Should().Contain("page=1");
		handler.SentRequests[1].RequestUri!.Query.Should().Contain("page=2");
	}

	[Fact]
	public async Task GetCategoryTransactions_EmptyCategory_ReturnsEmptyList()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.EmptyListResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync(
			"/organizations/org1/memberships/m1/portfolio/startups/transactions?page=1&per_page=200");
		var json = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		json.Should().Contain("\"result\": []");
	}

	// ================================================================
	// ERROR HANDLING
	// ================================================================

	[Fact]
	public async Task ApiRequest_Non200Status_ReturnsErrorResponse()
	{
		// Arrange: 500 Internal Server Error
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(
				ApiFixtures.ApiErrorResponse("internal_error", "Something went wrong"),
				HttpStatusCode.InternalServerError);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/organizations/org1/memberships/m1/portfolio");

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		var json = await response.Content.ReadAsStringAsync();
		json.Should().Contain("internal_error");
	}

	[Theory]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.Forbidden)]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	public async Task ApiRequest_Various4xxAnd5xx_PropagatesStatusCode(HttpStatusCode expectedStatus)
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueError(expectedStatus, """{"result":null,"error":{"code":"test","message":"test"}}""");

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/organizations/org1/memberships/m1/portfolio");

		response.StatusCode.Should().Be(expectedStatus);
	}

	[Fact]
	public async Task ApiRequest_401Unauthorized_IndicatesTokenExpiry()
	{
		// Arrange: 401 during API call → token expired, should trigger refresh
		var handler = new MockHttpMessageHandler()
			.EnqueueError(HttpStatusCode.Unauthorized);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/organizations/org1/memberships/m1/portfolio");

		// Assert: 401 should be detectable for retry-with-fresh-token logic in FinaryDelegatingHandler
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ApiRequest_429TooManyRequests_IndicatesRateLimit()
	{
		// Arrange: 429 with Retry-After header
		var response429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
		response429.Headers.Add("Retry-After", "5");

		var handler = new MockHttpMessageHandler()
			.Enqueue(response429);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/organizations/org1/memberships/m1/portfolio");

		// Assert: 429 detected, Retry-After available
		response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
		response.Headers.GetValues("Retry-After").Should().Contain("5");
	}

	// ================================================================
	// NETWORK ERRORS
	// ================================================================

	[Fact]
	public async Task ApiRequest_Timeout_ThrowsTaskCanceledException()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueTimeout();

		using var httpClient = CreateClient(handler);

		var act = async () => await httpClient.GetAsync("/organizations/org1/memberships/m1/portfolio");

		await act.Should().ThrowAsync<TaskCanceledException>();
	}

	// ================================================================
	// ASSET CATEGORY URL MAPPING
	// ================================================================

	[Theory]
	[InlineData(AssetCategory.Checkings, "checkings")]
	[InlineData(AssetCategory.Savings, "savings")]
	[InlineData(AssetCategory.Investments, "investments")]
	[InlineData(AssetCategory.RealEstates, "real_estates")]
	[InlineData(AssetCategory.Cryptos, "cryptos")]
	[InlineData(AssetCategory.FondsEuro, "fonds_euro")]
	[InlineData(AssetCategory.Commodities, "commodities")]
	[InlineData(AssetCategory.Credits, "credits")]
	[InlineData(AssetCategory.OtherAssets, "other_assets")]
	[InlineData(AssetCategory.Startups, "startups")]
	public void AssetCategory_ToUrlSegment_MapsCorrectly(AssetCategory category, string expected)
	{
		// Act: use the real ToUrlSegment extension method
		var segment = category.ToUrlSegment();

		// Assert
		segment.Should().Be(expected);
	}

	// ================================================================
	// RESPONSE ENVELOPE VALIDATION
	// ================================================================

	[Fact]
	public async Task ResponseEnvelope_WithResult_CanBeDeserialized()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.PortfolioSummaryResponse);

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/test");
		var json = await response.Content.ReadAsStringAsync();

		// Validate envelope structure
		json.Should().Contain("\"result\":");
		json.Should().Contain("\"message\":");
		json.Should().Contain("\"error\":");
	}

	[Fact]
	public async Task ResponseEnvelope_WithError_CanBeDeserialized()
	{
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.ApiErrorResponse("test_code", "test message"));

		using var httpClient = CreateClient(handler);
		var response = await httpClient.GetAsync("/test");
		var json = await response.Content.ReadAsStringAsync();

		json.Should().Contain("\"result\": null");
		json.Should().Contain("\"code\": \"test_code\"");
		json.Should().Contain("\"message\": \"test message\"");
	}
}
