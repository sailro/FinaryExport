using FluentAssertions;
using Moq;
using FinaryExport.Auth;
using FinaryExport.Configuration;
using FinaryExport.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinaryExport.Tests.Auth;

// Tests that ClerkAuthClient guards against refresh before login.
public sealed class ClerkAuthClientSkipTests
{
    private static ClerkAuthClient CreateClient()
    {
        var store = new InMemorySessionStore();
        var prompt = new Mock<ICredentialPrompt>();
        var options = Options.Create(new FinaryOptions());
        var logger = NullLogger<ClerkAuthClient>.Instance;
        return new ClerkAuthClient(store, prompt.Object, options, logger);
    }

    [Fact]
    public void SessionId_InitiallyEmpty()
    {
        using var client = CreateClient();
        client.SessionId.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsWhenSessionIdEmpty()
    {
        using var client = CreateClient();

        var act = () => client.RefreshTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot refresh token before login*");
    }
}
