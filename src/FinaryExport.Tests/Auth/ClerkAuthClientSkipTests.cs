using FinaryExport.Auth;
using FinaryExport.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinaryExport.Tests.Auth;

// Tests that ClerkAuthClient guards against refresh before login.
public sealed class ClerkAuthClientSkipTests
{
	private static ClerkAuthClient CreateClient()
	{
		var store = new InMemorySessionStore();
		var prompt = new Mock<ICredentialPrompt>();
		prompt.Setup(p => p.PromptCredentialsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(("test@example.com", "password", "123456"));
		var logger = NullLogger<ClerkAuthClient>.Instance;
		return new ClerkAuthClient(store, prompt.Object, logger);
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
