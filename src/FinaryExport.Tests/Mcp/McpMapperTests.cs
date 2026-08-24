using System.Text.Json;
using FinaryExport.Mcp.Contracts;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using FinaryExport.Models.User;
using FluentAssertions;

namespace FinaryExport.Tests.Mcp;

public sealed class McpMapperTests
{
	[Fact]
	public void GlobalResponses_UseScopesDistinctFromPortfolioData()
	{
		var user = McpMapper.User(new UserProfile { Fullname = "Test User" });
		var profiles = McpMapper.Profiles(
			[new FinaryProfile("organization", "membership", "Profile")]);
		var portfolio = McpMapper.Portfolio(null, "all", "EUR");

		user.Scope.Should().Be("authenticated_user");
		profiles.Scope.Should().Be("available_profiles");
		portfolio.Scope.Should().Be("active_profile");
	}

	[Fact]
	public void Accounts_DeduplicatesCategoryAppearancesAndPreservesIban()
	{
		var account = new Account
		{
			Id = "shared-account",
			Name = "Shared account",
			Iban = "FR761234567890",
			DisplayBalance = 1234.5678m,
			OwnershipRepartition =
			[
				new OwnershipEntry
				{
					Share = 0.5m,
					Membership = new OwnershipMembership { Id = "private-member" }
				}
			]
		};

		var response = McpMapper.Accounts(
			[("checkings", account), ("savings", account with { DisplayBalance = 1000m })],
			"all",
			"EUR",
			0,
			100);
		var json = JsonSerializer.Serialize(response);

		response.TotalUniqueAccounts.Should().Be(1);
		response.Accounts.Single().CategoryValues.Should().HaveCount(2);
		response.Accounts.Single().CategoryValues[0].DisplayBalance.Should().Be("1234.5678");
		response.Accounts.Single().Iban.Should().Be("FR761234567890");
		json.Should().Contain("\"iban\":\"FR761234567890\"");
		json.Should().NotContain("private-member");
		json.Should().NotContain("ownership");
	}

	[Fact]
	public void Timeseries_NormalizesTupleDataAndPagesPoints()
	{
		using var document = JsonDocument.Parse(
			"""[["2026-08-20",100.125],["2026-08-21",101.5],["2026-08-22",102]]""");
		var source = new TimeseriesData
		{
			Label = "Checking accounts",
			DisplayAmount = 102m,
			Timeseries = document.RootElement.Clone()
		};

		var response = McpMapper.Timeseries([source], "1m", "EUR", 1, 1);

		response.TotalPoints.Should().Be(3);
		response.HasMore.Should().BeTrue();
		response.Points.Should().ContainSingle();
		response.Points[0].Date.Should().Be("2026-08-21");
		response.Points[0].Value.Should().Be("101.5");
	}

	[Fact]
	public void Accounts_UsesConnectionSynchronizationMetadataWhenAvailable()
	{
		var account = new Account
		{
			Id = "account",
			ConnectionId = "connection",
			State = "stale",
			LastSuccessfulSyncAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
		};
		var synchronization = new AccountSynchronization
		{
			CorrelationId = "connection",
			State = "succeeded",
			StateMessage = "Up to date",
			SyncStatus = "completed",
			LastSyncAt = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero),
			LastSuccessfulSyncAt = new DateTimeOffset(2026, 8, 24, 9, 59, 0, TimeSpan.Zero)
		};

		var result = McpMapper.Accounts(
			[("checkings", account)],
			"all",
			"EUR",
			0,
			100,
			synchronizations: [synchronization]);

		var item = result.Accounts.Single();
		item.State.Should().Be("succeeded");
		item.StateMessage.Should().Be("Up to date");
		item.SyncStatus.Should().Be("completed");
		item.LastSyncAt.Should().Be("2026-08-24T10:00:00.0000000+00:00");
		item.LastSuccessfulSyncAt.Should().Be("2026-08-24T09:59:00.0000000+00:00");
	}

	[Fact]
	public void InvestmentAccount_ContainsSecurityFiatAndScpiPositions()
	{
		using var scpis = JsonDocument.Parse(
			"""[{"quantity":4,"display_current_value":1200,"display_current_price":300,"scpi":{"name":"SCPI Test","type":"scpi"}}]""");
		var account = new Account
		{
			Id = "investment-account",
			Name = "PEA",
			DisplayBalance = 2750m,
			Securities =
			[
				new SecurityPosition
				{
					Quantity = 12.25m,
					DisplayCurrentValue = 2750m,
					Security = new SecurityInfo
					{
						Name = "World ETF",
						Isin = "TEST00000001",
						SecurityType = "etf"
					}
				}
			],
			Fiats =
			[
				new CurrencyPosition
				{
					Quantity = 25m,
					DisplayCurrentValue = 25m,
					Fiat = new AssetInfo { Name = "Euro", Code = "EUR" }
				}
			],
			Scpis = scpis.RootElement.Clone()
		};

		var result = McpMapper.InvestmentAccount(account);

		result.Positions.Should().HaveCount(3);
		result.Positions.Should().ContainEquivalentOf(new
		{
			Kind = "security",
			Name = "World ETF",
			Quantity = "12.25",
			CurrentValue = "2750"
		});
		result.Positions.Should().ContainEquivalentOf(new
		{
			Kind = "fiat",
			Name = "Euro",
			Code = "EUR",
			CurrentValue = "25"
		});
		result.Positions.Should().ContainEquivalentOf(new
		{
			Kind = "scpi",
			Name = "SCPI Test",
			Quantity = "4",
			CurrentPrice = "300",
			CurrentValue = "1200"
		});
	}

	[Fact]
	public void Portfolio_DropsRawOwnershipAndInternalMetadata()
	{
		using var assets = JsonDocument.Parse(
			"""{"checking_accounts":{"display_amount":1500,"share":10,"ownership_repartition":[{"membership":{"id":"private-member"}}],"intercom_secure_hash":"secret"}}""");
		var summary = new PortfolioSummary
		{
			Gross = new PortfolioValues
			{
				Total = new PortfolioTotalValues { DisplayAmount = 1500.25m },
				Assets = assets.RootElement.Clone()
			},
			Net = new PortfolioValues { Total = new PortfolioTotalValues { DisplayAmount = 1400m } },
			Finary = new PortfolioValues { Total = new PortfolioTotalValues { DisplayAmount = 500m } }
		};

		var response = McpMapper.Portfolio(summary, "all", "EUR");
		var json = JsonSerializer.Serialize(response);

		response.GrossAssets.Amount.Should().Be("1500.25");
		response.Categories.Should().ContainSingle().Which.Category.Should().Be("checkings");
		json.Should().NotContain("private-member");
		json.Should().NotContain("secure_hash");
		json.Should().NotContain("ownership_repartition");
	}
}
