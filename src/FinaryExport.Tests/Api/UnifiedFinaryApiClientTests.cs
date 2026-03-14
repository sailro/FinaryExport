using FluentAssertions;
using Moq;
using FinaryExport.Api;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using FinaryExport.Models.Transactions;
using FinaryExport.Models.User;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinaryExport.Tests.Api;

// Tests for UnifiedFinaryApiClient — the decorator that aggregates data
// from ALL memberships for the unified export.
public sealed class UnifiedFinaryApiClientTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    private static readonly FinaryProfile Owner =
        new("org1", "membership-owner", "Jean Dupont");
    private static readonly FinaryProfile Member2 =
        new("org1", "membership-Marie", "Marie Dupont");
    private static readonly FinaryProfile Member3 =
        new("org1", "membership-Claire", "Claire Dupont");

    private static readonly List<FinaryProfile> AllProfiles = [Owner, Member2, Member3];

    // ================================================================
    // CONSTRUCTOR VALIDATION
    // ================================================================

    [Fact]
    public void Constructor_EmptyProfiles_Throws()
    {
        var mock = new Mock<IFinaryApiClient>();
        var act = () => new UnifiedFinaryApiClient(mock.Object, [], _logger);
        act.Should().Throw<ArgumentException>().WithParameterName("profiles");
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        var act = () => new UnifiedFinaryApiClient(null!, AllProfiles, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("inner");
    }

    // ================================================================
    // ACCOUNT MERGING — CORE LOGIC
    // ================================================================

    [Fact]
    public async Task GetCategoryAccounts_SharedAsset_DeduplicatedById()
    {
        // Shared real estate appears in both memberships with same Balance
        var sharedId = "acc-shared-1";

        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.RealEstates, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                return ctx.CurrentMembershipId switch
                {
                    "membership-owner" => Task.FromResult(new List<Account>
                    {
                        new()
                        {
                            Id = sharedId, Name = "12 Rue de la Paix, 75002 Paris, France",
                            Balance = 790200m, DisplayBalance = 687474m,
                            OwnershipRepartition =
                            [
                                new() { Share = 0.87m, Membership = new() { Id = "membership-owner" } },
                                new() { Share = 0.13m, Membership = new() { Id = "membership-Marie" } },
                            ]
                        }
                    }),
                    "membership-Marie" => Task.FromResult(new List<Account>
                    {
                        new()
                        {
                            Id = sharedId, Name = "12 Rue de la Paix, 75002 Paris, France",
                            Balance = 790200m, DisplayBalance = 102726m,
                            OwnershipRepartition =
                            [
                                new() { Share = 0.87m, Membership = new() { Id = "membership-owner" } },
                                new() { Share = 0.13m, Membership = new() { Id = "membership-Marie" } },
                            ]
                        }
                    }),
                    _ => Task.FromResult(new List<Account>())
                };
            });

        var result = await client.GetCategoryAccountsAsync(AssetCategory.RealEstates);

        result.Should().HaveCount(1, "shared asset should appear once");
        result[0].Id.Should().Be(sharedId);
        result[0].Balance.Should().Be(790200m, "full value is kept");
    }

    [Fact]
    public async Task GetCategoryAccounts_ExclusiveAssets_AllIncluded()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                return ctx.CurrentMembershipId switch
                {
                    "membership-owner" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "acc-jean-1", Name = "Jean Livret A", Balance = 10000m }
                    }),
                    "membership-Marie" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "acc-Marie-1", Name = "Marie Livret A", Balance = 5000m }
                    }),
                    "membership-Claire" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "acc-Claire-1", Name = "Claire LEP", Balance = 3000m }
                    }),
                    _ => Task.FromResult(new List<Account>())
                };
            });

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Savings);

        result.Should().HaveCount(3, "all exclusive accounts should be included");
        result.Select(a => a.Id).Should().BeEquivalentTo(["acc-jean-1", "acc-Marie-1", "acc-Claire-1"]);
        result.Sum(a => a.Balance ?? 0m).Should().Be(18000m, "10k + 5k + 3k");
    }

    [Fact]
    public async Task GetCategoryAccounts_MixSharedAndExclusive_MergedCorrectly()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                return ctx.CurrentMembershipId switch
                {
                    "membership-owner" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "shared-1", Name = "Joint Checking", Balance = 5000m, DisplayBalance = 2500m },
                        new() { Id = "owner-only", Name = "Jean PEL", Balance = 8000m }
                    }),
                    "membership-Marie" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "shared-1", Name = "Joint Checking", Balance = 5000m, DisplayBalance = 2500m },
                        new() { Id = "Marie-only", Name = "Marie LEP", Balance = 4000m }
                    }),
                    _ => Task.FromResult(new List<Account>())
                };
            });

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Checkings);

        result.Should().HaveCount(3, "shared-1 once + 2 exclusives");
        result.Select(a => a.Id).Should().BeEquivalentTo(["shared-1", "owner-only", "Marie-only"]);
    }

    [Fact]
    public async Task GetCategoryAccounts_NullId_Skipped()
    {
        var (mock, client) = CreateContextTrackingClient([Owner], out _);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
			[
				new() { Id = "valid-1", Name = "Valid", Balance = 1000m },
                new() { Id = null, Name = "No ID", Balance = 500m }
            ]);

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Checkings);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("valid-1");
    }

    [Fact]
    public async Task GetCategoryAccounts_FirstSeenWins()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                return ctx.CurrentMembershipId switch
                {
                    "membership-owner" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "acc-1", Name = "Test", Balance = 100m }
                    }),
                    "membership-Marie" => Task.FromResult(new List<Account>
                    {
                        new() { Id = "acc-1", Name = "Test", Balance = 200m }
                    }),
                    _ => Task.FromResult(new List<Account>())
                };
            });

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Savings);

        result.Should().HaveCount(1);
        result[0].Balance.Should().Be(100m, "first-seen version (owner) is kept");
    }

    [Fact]
    public async Task GetCategoryAccounts_ResultIsCached()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out _);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new() { Id = "a1", Balance = 100m }]);

        var result1 = await client.GetCategoryAccountsAsync(AssetCategory.Savings);
        var result2 = await client.GetCategoryAccountsAsync(AssetCategory.Savings);

        result1.Should().BeSameAs(result2, "second call should return cached list");

        // Inner client called once per profile (3), not twice (6)
        mock.Verify(
            x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ================================================================
    // OWNERSHIP SCALING
    // ================================================================

    [Fact]
    public async Task GetCategoryAccounts_OwnershipScaling_ComputesFullBalance()
    {
        // Owner has 60% share → DisplayBalance is 60% of full balance
        // Production: fullBalance = DisplayBalance / share = 6000 / 0.60 = 10000
        var (mock, client) = CreateContextTrackingClient([Owner], out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.RealEstates, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(new List<Account>
            {
                new()
                {
                    Id = "prop-1", Name = "Apartment",
                    Balance = 10000m, DisplayBalance = 6000m,
                    OwnershipRepartition =
                    [
                        new() { Share = 0.60m, Membership = new() { Id = "membership-owner" } },
                        new() { Share = 0.40m, Membership = new() { Id = "other-member" } }
                    ]
                }
            }));

        var result = await client.GetCategoryAccountsAsync(AssetCategory.RealEstates);

        result.Should().HaveCount(1);
        result[0].Balance.Should().Be(10000m, "6000 / 0.60 = 10000");
    }

    [Fact]
    public async Task GetCategoryAccounts_ExclusiveAsset_UsesDisplayBalanceDirectly()
    {
        // No OwnershipRepartition → exclusive asset → Balance = DisplayBalance ?? Balance
        var (mock, client) = CreateContextTrackingClient([Owner], out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(new List<Account>
            {
                new()
                {
                    Id = "savings-1", Name = "Livret",
                    Balance = 5000m, DisplayBalance = 4800m,
                    OwnershipRepartition = null
                }
            }));

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Savings);

        result.Should().HaveCount(1);
        result[0].Balance.Should().Be(4800m, "exclusive asset uses DisplayBalance when available");
    }

    [Fact]
    public async Task GetCategoryAccounts_FullOwnership_UsesDisplayBalanceDirectly()
    {
        // Share == 1.0 → not partial → uses DisplayBalance directly (else branch)
        var (mock, client) = CreateContextTrackingClient([Owner], out var ctx);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(new List<Account>
            {
                new()
                {
                    Id = "full-1", Name = "PEL",
                    Balance = 8000m, DisplayBalance = 8000m,
                    OwnershipRepartition =
                    [
                        new() { Share = 1.0m, Membership = new() { Id = "membership-owner" } }
                    ]
                }
            }));

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Savings);

        result.Should().HaveCount(1);
        result[0].Balance.Should().Be(8000m, "full ownership uses DisplayBalance directly");
    }

    // ================================================================
    // PORTFOLIO SUMMARY
    // ================================================================

    [Fact]
    public async Task GetPortfolio_GrossTotal_SumsAllMergedAccounts()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        SetupOwnerPortfolio(mock, 150000m, 140000m, evolution: 5000m);

        // Investments: 100k from owner, 30k exclusive from Marie
        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<Account> { new() { Id = "inv-1", Balance = 100000m } }),
                "membership-Marie" => Task.FromResult(new List<Account> { new() { Id = "inv-2", Balance = 30000m } }),
                _ => Task.FromResult(new List<Account>())
            });

        // Savings: 50k from owner
        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<Account> { new() { Id = "sav-1", Balance = 50000m } }),
                _ => Task.FromResult(new List<Account>())
            });

        // Empty all other categories
        foreach (var cat in Enum.GetValues<AssetCategory>())
        {
            if (cat is AssetCategory.Investments or AssetCategory.Savings) continue;
            mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        var portfolio = await client.GetPortfolioAsync();

        portfolio.Should().NotBeNull();
        portfolio!.Gross!.Total!.Amount.Should().Be(180000m, "100k + 30k + 50k");
        portfolio.Gross.Total.DisplayAmount.Should().Be(180000m);
    }

    [Fact]
    public async Task GetPortfolio_NetTotal_SubtractsCredits()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);
        SetupOwnerPortfolio(mock, 100000m, 80000m);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<Account> { new() { Id = "inv-1", Balance = 100000m } }),
                _ => Task.FromResult(new List<Account>())
            });

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Credits, It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<Account> { new() { Id = "credit-1", Balance = 20000m } }),
                _ => Task.FromResult(new List<Account>())
            });

        foreach (var cat in Enum.GetValues<AssetCategory>())
        {
            if (cat is AssetCategory.Investments or AssetCategory.Credits) continue;
            mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        var portfolio = await client.GetPortfolioAsync();

        portfolio!.Gross!.Total!.Amount.Should().Be(100000m, "investments only");
        portfolio.Net!.Total!.Amount.Should().Be(80000m, "100k - 20k credits");
    }

    [Fact]
    public async Task GetPortfolio_PreservesOwnerEvolution()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out _);
        SetupOwnerPortfolio(mock, 100000m, 90000m, evolution: 5000m, evolutionPct: 5.5m);

        foreach (var cat in Enum.GetValues<AssetCategory>())
        {
            mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        var portfolio = await client.GetPortfolioAsync();

        portfolio!.Gross!.Total!.Evolution.Should().Be(5000m);
        portfolio.Gross.Total.EvolutionPercent.Should().Be(5.5m);
    }

    // ================================================================
    // TRANSACTIONS
    // ================================================================

    [Fact]
    public async Task GetCategoryTransactions_SharedTransactions_DeduplicatedById()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<Transaction>
                {
                    new() { Id = 1001, Name = "Shared TX", Date = "2025-01-15", Value = 500m },
                    new() { Id = 1002, Name = "Owner TX", Date = "2025-01-10", Value = 200m }
                }),
                "membership-Marie" => Task.FromResult(new List<Transaction>
                {
                    new() { Id = 1001, Name = "Shared TX", Date = "2025-01-15", Value = 500m },
                    new() { Id = 1003, Name = "Marie TX", Date = "2025-01-12", Value = 300m }
                }),
                _ => Task.FromResult(new List<Transaction>())
            });

        var result = await client.GetCategoryTransactionsAsync(AssetCategory.Checkings);

        result.Should().HaveCount(3, "shared TX appears once + 2 exclusives");
        result.Select(t => t.Id).Should().BeEquivalentTo([1001L, 1002L, 1003L]);
    }

    [Fact]
    public async Task GetCategoryTransactions_SortedByDateDescending()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<Transaction>
                {
                    new() { Id = 1, Date = "2025-01-01", Value = 100m },
                    new() { Id = 2, Date = "2025-03-01", Value = 200m }
                }),
                "membership-Marie" => Task.FromResult(new List<Transaction>
                {
                    new() { Id = 3, Date = "2025-02-01", Value = 300m }
                }),
                _ => Task.FromResult(new List<Transaction>())
            });

        var result = await client.GetCategoryTransactionsAsync(AssetCategory.Checkings);

        result.Select(t => t.Date).Should().BeInDescendingOrder();
    }

    // ================================================================
    // DIVIDENDS
    // ================================================================

    [Fact]
    public async Task GetPortfolioDividends_MergesPastDividendsById()
    {
        var sharedDiv = new DividendEntry { Id = 101, Amount = 500m, AssetType = "SCPI" };
        var ownerDiv = new DividendEntry { Id = 102, Amount = 200m, AssetType = "ETF" };
        var memberDiv = new DividendEntry { Id = 103, Amount = 100m, AssetType = "Bond" };

        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetPortfolioDividendsAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult<DividendSummary?>(new DividendSummary
                {
                    AnnualIncome = 2000m, PastIncome = 700m, Yield = 3.5m,
                    PastDividends = [sharedDiv, ownerDiv], UpcomingDividends = []
                }),
                "membership-Marie" => Task.FromResult<DividendSummary?>(new DividendSummary
                {
                    AnnualIncome = 600m, PastIncome = 600m, Yield = 2.0m,
                    PastDividends = [sharedDiv, memberDiv], UpcomingDividends = []
                }),
                _ => Task.FromResult<DividendSummary?>(null)
            });

        var result = await client.GetPortfolioDividendsAsync();

        result.Should().NotBeNull();
        result!.PastDividends.Should().HaveCount(3, "3 unique dividend entries");
        result.PastIncome.Should().Be(800m, "500 + 200 + 100 from merged entries");
        result.AnnualIncome.Should().Be(2000m, "owner's AnnualIncome preserved");
        result.Yield.Should().Be(3.5m, "owner's Yield preserved");
    }

    [Fact]
    public async Task GetPortfolioDividends_AllNull_ReturnsNull()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out _);

        mock.Setup(x => x.GetPortfolioDividendsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DividendSummary?)null);

        var result = await client.GetPortfolioDividendsAsync();
        result.Should().BeNull();
    }

    // ================================================================
    // HOLDINGS ACCOUNTS
    // ================================================================

    [Fact]
    public async Task GetHoldingsAccounts_MergedById()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out var ctx);

        mock.Setup(x => x.GetHoldingsAccountsAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ctx.CurrentMembershipId switch
            {
                "membership-owner" => Task.FromResult(new List<HoldingsAccount>
                {
                    new() { Id = "ha-1", Name = "PEA BNP", Balance = 50000m }
                }),
                "membership-Marie" => Task.FromResult(new List<HoldingsAccount>
                {
                    new() { Id = "ha-1", Name = "PEA BNP", Balance = 50000m },
                    new() { Id = "ha-2", Name = "Marie CTO", Balance = 10000m }
                }),
                _ => Task.FromResult(new List<HoldingsAccount>())
            });

        var result = await client.GetHoldingsAccountsAsync();

        result.Should().HaveCount(2);
        result.Select(a => a.Id).Should().BeEquivalentTo(["ha-1", "ha-2"]);
    }

    // ================================================================
    // CONTEXT MANAGEMENT
    // ================================================================

    [Fact]
    public void SetOrganizationContext_IsNoOp()
    {
        var mock = new Mock<IFinaryApiClient>();
        var client = new UnifiedFinaryApiClient(mock.Object, AllProfiles, _logger);

        // Should not throw, and should not propagate to inner
        client.SetOrganizationContext("any", "any");

        mock.Verify(x => x.SetOrganizationContext("any", "any"), Times.Never);
    }

    [Fact]
    public async Task DelegatedMethods_UseOwnerContext()
    {
        var (mock, client) = CreateContextTrackingClient(AllProfiles, out _);

        mock.Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile());

        await client.GetCurrentUserAsync();

        mock.Verify(x => x.SetOrganizationContext(Owner.OrgId, Owner.MembershipId), Times.AtLeastOnce);
    }

    // ================================================================
    // SINGLE PROFILE (edge case)
    // ================================================================

    [Fact]
    public async Task SingleProfile_BehavesLikePassthrough()
    {
        var (mock, client) = CreateContextTrackingClient([Owner], out _);

        mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new() { Id = "a1", Name = "Test", Balance = 1000m }]);

        var result = await client.GetCategoryAccountsAsync(AssetCategory.Savings);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a1");
    }

    // ================================================================
    // HELPERS
    // ================================================================

    // Creates a mock IFinaryApiClient that tracks SetOrganizationContext calls
    // so per-profile data routing works correctly.
    private (Mock<IFinaryApiClient> mock, UnifiedFinaryApiClient client) CreateContextTrackingClient(
        List<FinaryProfile> profiles, out ContextState ctx)
    {
        var mock = new Mock<IFinaryApiClient>();
        var state = new ContextState();

        mock.Setup(x => x.SetOrganizationContext(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, mid) => state.CurrentMembershipId = mid);

        var client = new UnifiedFinaryApiClient(mock.Object, profiles, _logger);
        ctx = state;
        return (mock, client);
    }

    // Tracks which membership context is currently active
    internal sealed class ContextState
    {
        public string? CurrentMembershipId { get; set; }
    }

    private static void SetupOwnerPortfolio(
        Mock<IFinaryApiClient> mock, decimal gross, decimal net,
        decimal? evolution = null, decimal? evolutionPct = null)
    {
        mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioSummary
            {
                Gross = new PortfolioValues
                {
                    Total = new PortfolioTotalValues
                    {
                        Amount = gross,
                        DisplayAmount = gross,
                        Evolution = evolution,
                        EvolutionPercent = evolutionPct,
                    }
                },
                Net = new PortfolioValues
                {
                    Total = new PortfolioTotalValues
                    {
                        Amount = net,
                        DisplayAmount = net,
                    }
                }
            });
    }
}
