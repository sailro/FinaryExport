using System.Net;
using System.Net.Http.Headers;
using FinaryExport.Api;
using FinaryExport.Auth;
using FinaryExport.Infrastructure;
using FinaryExport.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinaryExport.Tests.Infrastructure;

public sealed class FinaryDelegatingHandlerTests
{
	private static FinaryDelegatingHandler CreateHandler(
		Mock<ITokenProvider> tokenProvider,
		MockHttpMessageHandler inner)
	{
		var handler = new FinaryDelegatingHandler(
			tokenProvider.Object,
			new RateLimiter(),
			NullLogger<FinaryDelegatingHandler>.Instance)
		{
			InnerHandler = inner
		};
		return handler;
	}

	[Fact]
	public async Task SendAsync_InjectsRequiredHeaders()
	{
		var inner = new MockHttpMessageHandler()
			.EnqueueJson("{}");
		var tokenMock = new Mock<ITokenProvider>();
		tokenMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("test-jwt-token");

		using var handler = CreateHandler(tokenMock, inner);
		using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.finary.com") };

		await client.GetAsync("/test");

		var req = inner.SentRequests[0];
		req.Headers.Authorization.Should().NotBeNull();
		req.Headers.Authorization!.Scheme.Should().Be("Bearer");
		req.Headers.Authorization.Parameter.Should().Be("test-jwt-token");
		req.Headers.GetValues("Origin").Should().Contain("https://app.finary.com");
		req.Headers.GetValues("Referer").Should().Contain("https://app.finary.com/");
		req.Headers.GetValues("x-client-api-version").Should().Contain("2");
		req.Headers.GetValues("x-finary-client-id").Should().Contain("webapp");
	}

	[Fact]
	public async Task SendAsync_On401_RetriesWithNewToken()
	{
		var inner = new MockHttpMessageHandler()
			.EnqueueError(HttpStatusCode.Unauthorized)
			.EnqueueJson("{\"ok\":true}");

		var callCount = 0;
		var tokenMock = new Mock<ITokenProvider>();
		tokenMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => ++callCount == 1 ? "old-token" : "refreshed-token");

		using var handler = CreateHandler(tokenMock, inner);
		using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.finary.com") };

		var response = await client.GetAsync("/data");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		inner.SentRequests.Should().HaveCount(2);
		// Second request should use refreshed token
		inner.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("refreshed-token");
	}

	[Fact]
	public async Task SendAsync_On429_RetriesWithBackoff()
	{
		var retryResponse = new HttpResponseMessage((HttpStatusCode)429);
		retryResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));

		var inner = new MockHttpMessageHandler()
			.Enqueue(retryResponse)
			.EnqueueJson("{\"ok\":true}");

		var tokenMock = new Mock<ITokenProvider>();
		tokenMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("jwt");

		using var handler = CreateHandler(tokenMock, inner);
		using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.finary.com") };

		var response = await client.GetAsync("/limited");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		inner.SentRequests.Should().HaveCount(2);
	}

	[Fact]
	public async Task SendAsync_On429_MaxThreeRetries()
	{
		var tokenMock = new Mock<ITokenProvider>();
		tokenMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("jwt");

		var inner = new MockHttpMessageHandler();
		// Initial request + 3 retries = 4 responses needed
		for (var i = 0; i < 4; i++)
		{
			var resp = new HttpResponseMessage((HttpStatusCode)429);
			resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
			inner.Enqueue(resp);
		}

		using var handler = CreateHandler(tokenMock, inner);
		using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.finary.com") };

		var response = await client.GetAsync("/flood");

		response.StatusCode.Should().Be((HttpStatusCode)429, "still rate limited after max retries");
		// 1 initial + 3 retries = 4 total
		inner.SentRequests.Should().HaveCount(4);
	}

	[Fact]
	public async Task SendAsync_SuccessResponse_NoRetries()
	{
		var inner = new MockHttpMessageHandler()
			.EnqueueJson("{\"data\":\"ok\"}");

		var tokenMock = new Mock<ITokenProvider>();
		tokenMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("jwt");

		using var handler = CreateHandler(tokenMock, inner);
		using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.finary.com") };

		var response = await client.GetAsync("/ok");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		inner.SentRequests.Should().HaveCount(1);
	}
}
