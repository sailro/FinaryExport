using System.Text.Json;
using FinaryExport.Api;
using FinaryExport.Mcp;
using FinaryExport.Mcp.Tools;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.User;
using FluentAssertions;
using ModelContextProtocol;
using Moq;

namespace FinaryExport.Tests.Mcp;

public sealed class HoldingsToolsTests
{
	[Fact]
	public async Task GetHoldings_PagesAcrossAllModeledInvestmentPositionKinds()
	{
		using var scpis = JsonDocument.Parse(
			"""[{"quantity":4,"display_current_value":1200,"scpi":{"name":"SCPI Test"}}]""");
		var accounts = new List<Account>
		{
			new()
			{
				Id = "first",
				Name = "First",
				Securities = [new SecurityPosition { Security = new SecurityInfo { Name = "ETF" } }],
				Fiats = [new CurrencyPosition { Fiat = new AssetInfo { Name = "Euro" } }],
				Scpis = scpis.RootElement.Clone()
			},
			new()
			{
				Id = "second",
				Name = "Second",
				Securities = [new SecurityPosition { Security = new SecurityInfo { Name = "Stock" } }]
			}
		};
		var api = new Mock<IFinaryApiClient>();
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.Investments,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);
		api.Setup(item => item.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new UserProfile());
		var factory = new Mock<IMcpFinaryApiClientFactory>();
		factory.Setup(item => item.CreateActiveClientAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(api.Object);

		var result = await new HoldingsTools(factory.Object).GetHoldings(1, 2);

		result.AccountCount.Should().Be(2);
		result.PositionCount.Should().Be(4);
		result.ReturnedPositionCount.Should().Be(2);
		result.HasMore.Should().BeTrue();
		result.Accounts.Should().ContainSingle().Which.Positions
			.Select(item => item.Kind).Should().Equal("fiat", "scpi");
	}

	[Fact]
	public async Task GetAccountPositions_MissingAccountReturnsActionableMcpError()
	{
		var api = new Mock<IFinaryApiClient>();
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.Investments,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);
		var factory = new Mock<IMcpFinaryApiClientFactory>();
		factory.Setup(item => item.CreateActiveClientAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(api.Object);

		var act = () => new HoldingsTools(factory.Object)
			.GetAccountPositions("missing", "investments");

		await act.Should().ThrowAsync<McpException>()
			.WithMessage("*was not found*Use get_accounts*");
	}
}
