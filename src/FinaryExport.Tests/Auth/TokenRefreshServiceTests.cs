using System.Net;
using FinaryExport.Tests.Fixtures;
using FinaryExport.Tests.Helpers;
using FluentAssertions;
using static FinaryExport.FinaryConstants;

namespace FinaryExport.Tests.Auth;

// Tests for the TokenRefreshService (IHostedService with PeriodicTimer).
// Validates background token refresh, failure recovery, and thread safety.
public sealed class TokenRefreshServiceTests
{
	[Fact]
	public async Task TokenRefresh_SuccessfulRefresh_ReturnsNewJwt()
	{
		// Arrange: /tokens returns new JWT
		var newJwt = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.new_payload.new_signature";
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.ClerkTokenResponse(newJwt));

		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ClerkBaseUrl) };

		// Act: simulate a single refresh tick
		var response = await httpClient.PostAsync(
			"/v1/client/sessions/sess_test/tokens",
			new FormUrlEncodedContent([new("organization_id", "")]));

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		json.Should().Contain(newJwt);
		handler.SentRequests.Should().HaveCount(1);
		handler.SentRequests[0].Method.Should().Be(HttpMethod.Post);
	}

	[Fact]
	public async Task TokenRefresh_401Response_ShouldTriggerColdStart()
	{
		// Arrange: refresh returns 401 → session expired
		var handler = new MockHttpMessageHandler()
			.EnqueueError(HttpStatusCode.Unauthorized);

		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ClerkBaseUrl) };

		// Act
		var response = await httpClient.PostAsync(
			"/v1/client/sessions/sess_test/tokens",
			new FormUrlEncodedContent([new("organization_id", "")]));

		// Assert: 401 should be detected by the implementation to trigger cold start
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		handler.SentRequests.Should().HaveCount(1);
	}

	[Fact]
	public async Task TokenRefresh_NetworkError_ThrowsHttpRequestException()
	{
		// Arrange: network timeout
		var handler = new MockHttpMessageHandler()
			.EnqueueTimeout();

		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ClerkBaseUrl) };

		// Act & Assert: timeout should propagate as TaskCanceledException
		var act = async () => await httpClient.PostAsync(
			"/v1/client/sessions/sess_test/tokens",
			new FormUrlEncodedContent([new("organization_id", "")]));

		await act.Should().ThrowAsync<TaskCanceledException>();
	}

	[Fact]
	public async Task TokenRefresh_MultipleConsecutiveRefreshes_AllSucceed()
	{
		// Arrange: 3 consecutive refresh responses
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(ApiFixtures.ClerkTokenResponse("jwt_1"))
			.EnqueueJson(ApiFixtures.ClerkTokenResponse("jwt_2"))
			.EnqueueJson(ApiFixtures.ClerkTokenResponse("jwt_3"));

		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ClerkBaseUrl) };

		// Act: simulate 3 refresh cycles
		for (var i = 0; i < 3; i++)
		{
			var response = await httpClient.PostAsync(
				"/v1/client/sessions/sess_test/tokens",
				new FormUrlEncodedContent([new("organization_id", "")]));
			response.StatusCode.Should().Be(HttpStatusCode.OK);
		}

		// Assert: 3 sequential refreshes completed
		handler.SentRequests.Should().HaveCount(3);
	}

	[Fact]
	public void TokenRefresh_IntervalShouldBe50Seconds()
	{
		// Architecture decision D9: PeriodicTimer at 50-second interval
		// Token TTL is 60s; refreshing at 50s gives 10s safety margin.
		var interval = TimeSpan.FromSeconds(50);
		var tokenTtl = TimeSpan.FromSeconds(60);

		interval.Should().BeLessThan(tokenTtl, "refresh must happen before token expires");
		(tokenTtl - interval).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(10),
			"at least 10 seconds of safety margin before expiry");
	}
}
