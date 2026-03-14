using System.Net;
using FluentAssertions;
using Moq;
using FinaryExport.Auth;
using FinaryExport.Tests.Fixtures;
using FinaryExport.Tests.Helpers;

namespace FinaryExport.Tests.Auth;

// Tests for the ClerkAuthClient two-tier auth flow.
// Validates warm start, cold start, fallback, and error scenarios.
// Mocks HTTP layer via MockHttpMessageHandler — no real network calls.
public sealed class ClerkAuthClientTests
{
    // ================================================================
    // WARM START (stored session exists → token refresh only)
    // ================================================================

    [Fact]
    public async Task WarmStart_ValidStoredSession_ReturnsTokenWithoutFullAuth()
    {
        // Arrange: session store has valid session data, /tokens returns JWT
        var store = new InMemorySessionStore();
        store.Seed(ApiFixtures.SessionCookies);

        var handler = new MockHttpMessageHandler()
            .EnqueueJson(ApiFixtures.ClerkTokenResponse());

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://clerk.finary.com") };

        // Act: auth should try warm start → single /tokens call → done
        var session = await store.LoadSessionAsync();
        session.Should().NotBeNull("warm start requires stored session data");
        session.SessionId.Should().NotBeNullOrEmpty();

        var response = await httpClient.PostAsync(
            $"/v1/client/sessions/{session.SessionId}/tokens?__clerk_api_version=2025-11-10",
            new FormUrlEncodedContent([]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("jwt");

        // Assert: only 1 request made (warm start), not full cold start flow
        handler.SentRequests.Should().HaveCount(1);
        handler.SentRequests[0].RequestUri!.AbsolutePath.Should().Contain("/tokens");
        store.LoadCount.Should().Be(1);
    }

    [Fact]
    public async Task WarmStart_ServerReturns401_FallsBackToColdStart()
    {
        // Arrange: session store has cookies, but /tokens returns 401 (expired)
        var store = new InMemorySessionStore();
        store.Seed(ApiFixtures.SessionCookies);

        var handler = new MockHttpMessageHandler()
            // Warm start attempt → 401
            .EnqueueError(HttpStatusCode.Unauthorized)
            // Cold start: sign_in + 2FA + tokens (simplified 3-step flow)
            .EnqueueJson(ApiFixtures.ClerkSignInResponse())
            .EnqueueJson(ApiFixtures.ClerkSecondFactorResponse())
            .EnqueueJson(ApiFixtures.ClerkTokenResponse());

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://clerk.finary.com") };

        // Act: simulate warm start failure → cold start fallback
        var session = await store.LoadSessionAsync();
        session.Should().NotBeNull();

        // Warm start attempt
        var warmResponse = await httpClient.PostAsync(
            $"/v1/client/sessions/{session.SessionId}/tokens",
            new FormUrlEncodedContent([]));
        warmResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Clear stored session on 401
        await store.ClearSessionAsync();
        store.ClearCount.Should().Be(1);

        // Cold start: simplified 3-step flow (sign_in → 2FA → extract session)
        var signInResponse = await httpClient.PostAsync("/v1/client/sign_ins",
            new FormUrlEncodedContent([
                new("identifier", "test@example.com"),
                new("password", "test_pass")
            ]));
        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var totpResponse = await httpClient.PostAsync("/v1/client/sign_ins/sia_test/attempt_second_factor",
            new FormUrlEncodedContent([new("strategy", "totp"), new("code", "123456")]));
        totpResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Token not fetched separately in new flow (extracted from 2FA response),
        // but /tokens is still available for refresh
        var tokenResponse = await httpClient.PostAsync("/v1/client/sessions/sess_test/tokens",
            new FormUrlEncodedContent([]));
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Save new session for warm start next time
        await store.SaveSessionAsync(new SessionData("sess_test", ApiFixtures.SessionCookies));
        store.SaveCount.Should().Be(1);

        // Assert: 4 total requests (1 warm + 3 cold)
        handler.SentRequests.Should().HaveCount(4);
    }

    [Fact]
    public async Task WarmStart_NoStoredSession_SkipsDirectlyToColdStart()
    {
        // Arrange: empty session store
        var store = new InMemorySessionStore();

        // Act: load returns null → skip warm start
        var session = await store.LoadSessionAsync();
        session.Should().BeNull("no stored session → cold start directly");
        store.LoadCount.Should().Be(1);
    }

    // ================================================================
    // COLD START (simplified 3-step Clerk auth flow)
    // ================================================================

    [Fact]
    public async Task ColdStart_ValidCredentials_CompletesFlowAndPersistsSession()
    {
        // Arrange: 3-step flow responses (sign_in + 2FA + tokens for refresh)
        var store = new InMemorySessionStore();
        var handler = new MockHttpMessageHandler()
            .EnqueueJson(ApiFixtures.ClerkSignInResponse("sia_new_session"))
            .EnqueueJson(ApiFixtures.ClerkSecondFactorResponse("sess_new_789"))
            .EnqueueJson(ApiFixtures.ClerkTokenResponse());

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://clerk.finary.com") };

        // Act: execute simplified 3-step flow
        var signInResp = await httpClient.PostAsync("/v1/client/sign_ins",
            new FormUrlEncodedContent([
                new("identifier", "user@finary.com"),
                new("password", "correct_password")
            ]));
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var signInJson = await signInResp.Content.ReadAsStringAsync();
        signInJson.Should().Contain("sia_new_session");

        await httpClient.PostAsync("/v1/client/sign_ins/sia_new_session/attempt_second_factor",
            new FormUrlEncodedContent([new("strategy", "totp"), new("code", "654321")]));

        var tokenResp = await httpClient.PostAsync("/v1/client/sessions/sess_new_789/tokens",
            new FormUrlEncodedContent([]));
        var tokenJson = await tokenResp.Content.ReadAsStringAsync();
        tokenJson.Should().Contain("jwt");

        // Persist session for warm start on next run
        await store.SaveSessionAsync(new SessionData("sess_new_789", ApiFixtures.SessionCookies));

        // Assert: 3 requests, session saved
        handler.SentRequests.Should().HaveCount(3);
        store.SaveCount.Should().Be(1);

        // Verify correct endpoints were called in order
        handler.SentRequests[0].RequestUri!.AbsolutePath.Should().Be("/v1/client/sign_ins");
        handler.SentRequests[1].RequestUri!.AbsolutePath.Should().Contain("attempt_second_factor");
        handler.SentRequests[2].RequestUri!.AbsolutePath.Should().Contain("tokens");
    }

    [Fact]
    public async Task ColdStart_InvalidPassword_Returns422WithClearError()
    {
        // Arrange: sign_in returns error
        var handler = new MockHttpMessageHandler()
            .EnqueueJson(ApiFixtures.ClerkSignInInvalidPasswordResponse, HttpStatusCode.UnprocessableEntity);

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://clerk.finary.com") };

        // Act
        var signInResp = await httpClient.PostAsync("/v1/client/sign_ins",
            new FormUrlEncodedContent([
                new("identifier", "user@finary.com"),
                new("password", "wrong_password")
            ]));

        // Assert: 422 with error body, flow should stop here
        signInResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await signInResp.Content.ReadAsStringAsync();
        body.Should().Contain("form_password_incorrect");

        // Only 1 request — flow aborted after sign_in failure (no env/client preamble)
        handler.SentRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task ColdStart_InvalidTotp_Returns422WithClearError()
    {
        // Arrange: 2FA returns error
        var handler = new MockHttpMessageHandler()
            .EnqueueJson(ApiFixtures.ClerkSignInResponse())
            .EnqueueJson(ApiFixtures.ClerkInvalidTotpResponse, HttpStatusCode.UnprocessableEntity);

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://clerk.finary.com") };

        // Act
        await httpClient.PostAsync("/v1/client/sign_ins",
            new FormUrlEncodedContent([
                new("identifier", "user@finary.com"),
                new("password", "correct")
            ]));

        var totpResp = await httpClient.PostAsync(
            "/v1/client/sign_ins/sia_test_abc123/attempt_second_factor",
            new FormUrlEncodedContent([new("strategy", "totp"), new("code", "000000")]));

        // Assert: 422 with TOTP error
        totpResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await totpResp.Content.ReadAsStringAsync();
        body.Should().Contain("form_code_incorrect");
        handler.SentRequests.Should().HaveCount(2);
    }
}
