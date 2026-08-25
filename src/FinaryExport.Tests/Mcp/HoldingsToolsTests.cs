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
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.RealEstates,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.FondsEuro,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);
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
	public async Task GetHoldings_MergesMovedScpisAndIncludesFondsEuroWithoutPhysicalPropertyRows()
	{
		using var scpis = JsonDocument.Parse(
			"""[{"shares":4,"current_value":1200,"scpi":{"name":"SCPI Test"}}]""");
		using var emptyPositions = JsonDocument.Parse("{}");
		var investment = new Account
		{
			Id = "shared",
			Name = "Investment wrapper",
			DisplayBalance = 1000m,
			Securities = [new SecurityPosition { Security = new SecurityInfo { Name = "ETF" } }]
		};
		var realEstate = new Account
		{
			Id = "shared",
			Name = "Investment wrapper",
			DisplayBalance = 1200m,
			Scpis = scpis.RootElement.Clone()
		};
		var physicalProperty = new Account
		{
			Id = "house",
			Name = "House",
			DisplayBalance = 300000m,
			Scpis = emptyPositions.RootElement.Clone()
		};
		var fondsEuro = new Account
		{
			Id = "life-insurance",
			Name = "Fonds euro",
			DisplayBalance = 500m,
			Currency = new AccountCurrency { Code = "EUR" },
			FondsEuro = emptyPositions.RootElement.Clone()
		};
		var api = CreateInvestmentApi(
			[investment],
			[realEstate, physicalProperty],
			[fondsEuro]);

		var result = await CreateTools(api.Object).GetHoldings(limit: 100);

		result.AccountCount.Should().Be(2);
		result.PositionCount.Should().Be(3);
		result.Accounts.Should().NotContain(account => account.AccountId == "house");
		var merged = result.Accounts.Single(account => account.AccountId == "shared");
		merged.DisplayBalance.Should().Be("2200");
		merged.Positions.Select(position => position.Kind).Should().Equal("security", "scpi");
		merged.Positions.Single(position => position.Kind == "scpi").Should().BeEquivalentTo(new
		{
			Name = "SCPI Test",
			Quantity = "4",
			CurrentValue = "1200"
		});
		var fund = result.Accounts.Single(account => account.AccountId == "life-insurance");
		fund.Positions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
		{
			Kind = "fonds_euro",
			Name = "Fonds euro",
			Code = "EUR",
			CurrentValue = "500"
		});
	}

	[Theory]
	[InlineData("investments")]
	[InlineData("real_estates")]
	public async Task GetAccountPositions_MergesInvestmentAndMovedRealEstateComponents(string category)
	{
		using var scpis = JsonDocument.Parse(
			"""[{"shares":2,"current_value":600,"scpi":{"name":"SCPI Test"}}]""");
		var investment = new Account
		{
			Id = "shared",
			Name = "Investment wrapper",
			DisplayBalance = 1000m,
			Securities = [new SecurityPosition { Security = new SecurityInfo { Name = "ETF" } }]
		};
		var realEstate = investment with
		{
			DisplayBalance = 600m,
			Securities = null,
			Scpis = scpis.RootElement.Clone()
		};
		var api = CreateInvestmentApi([investment], [realEstate], []);

		var result = await CreateTools(api.Object).GetAccountPositions("shared", category);

		result.Account.DisplayBalance.Should().Be("1600");
		result.Account.Positions.Select(position => position.Kind).Should().Equal("security", "scpi");
	}

	[Fact]
	public async Task GetAccountPositions_FondsEuroUsesGroundedAccountFallback()
	{
		var fondsEuro = new Account
		{
			Id = "life-insurance",
			Name = "Fonds euro",
			DisplayBalance = 500m
		};
		var api = CreateInvestmentApi([], [], [fondsEuro]);

		var result = await CreateTools(api.Object)
			.GetAccountPositions("life-insurance", "fonds_euro");

		result.PositionCount.Should().Be(1);
		result.Account.Positions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
		{
			Kind = "fonds_euro",
			Name = "Fonds euro",
			CurrentValue = "500"
		});
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
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.RealEstates,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.FondsEuro,
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

	private static Mock<IFinaryApiClient> CreateInvestmentApi(
		List<Account> investments,
		List<Account> realEstates,
		List<Account> fondsEuro)
	{
		var api = new Mock<IFinaryApiClient>();
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.Investments,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(investments);
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.RealEstates,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(realEstates);
		api.Setup(item => item.GetCategoryAccountsAsync(
				AssetCategory.FondsEuro,
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(fondsEuro);
		api.Setup(item => item.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new UserProfile());
		return api;
	}

	private static HoldingsTools CreateTools(IFinaryApiClient api)
	{
		var factory = new Mock<IMcpFinaryApiClientFactory>();
		factory.Setup(item => item.CreateActiveClientAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(api);
		return new HoldingsTools(factory.Object);
	}
}
