using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinaryExport.Tests.Export;

public sealed class AccountsSheetTests
{
	private static AccountsSheet CreateSheet() => new(NullLogger<AccountsSheet>.Instance);

	[Fact]
	public void SheetName_IsAccounts()
	{
		CreateSheet().SheetName.Should().Be("Accounts");
	}

	[Fact]
	public async Task WriteAsync_CreatesSheetPerCategory_WithAccounts()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "c1", Name = "BNP Checking", Balance = 4500m }]);
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "s1", Name = "Livret A", Balance = 10000m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		wb.Worksheets.Should().Contain(ws => ws.Name == "Checkings");
		wb.Worksheets.Should().Contain(ws => ws.Name == "Savings");
	}

	[Fact]
	public async Task WriteAsync_SkipsCategoriesWithNoAccounts()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "c1", Name = "BNP", Balance = 100m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		wb.Worksheets.Should().HaveCount(1);
		wb.Worksheets.First().Name.Should().Be("Checkings");
	}

	[Fact]
	public async Task WriteAsync_WritesCorrectHeaders()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "i1", Name = "PEA" }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Investments");
		ws.Cell("A1").Value.ToString().Should().Be("Name");
		ws.Cell("B1").Value.ToString().Should().Be("Institution");
		ws.Cell("C1").Value.ToString().Should().Be("Balance");
		ws.Cell("D1").Value.ToString().Should().Be("Native Currency");
		ws.Cell("E1").Value.ToString().Should().Be("Buying Value");
		ws.Cell("F1").Value.ToString().Should().Be("Unrealized P&L");
		ws.Cell("G1").Value.ToString().Should().Be("Annual Yield");
		ws.Cell("H1").Value.ToString().Should().Be("IBAN");
		ws.Cell("I1").Value.ToString().Should().Be("Opened At");
		ws.Cell("J1").Value.ToString().Should().Be("Last Sync");
	}

	[Fact]
	public async Task WriteAsync_WritesAccountData()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new()
				{
					Id = "c1",
					Name = "BNP Main",
					Balance = 4523.67m,
					DisplayBalance = 4500m,
					BuyingValue = 0m,
					DisplayBuyingValue = 0m,
					UnrealizedPnl = 0m,
					AnnualYield = 0m,
					Iban = "FR76300040000312345",
					OpenedAt = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
					LastSyncAt = new DateTimeOffset(2025, 3, 12, 0, 0, 0, TimeSpan.Zero),
					Institution = new AccountInstitution { Name = "BNP Paribas" },
					Currency = new AccountCurrency { Code = "EUR" }
				}
			]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Checkings");
		ws.Cell("A2").Value.ToString().Should().Be("BNP Main");
		ws.Cell("B2").Value.ToString().Should().Be("BNP Paribas");
		ws.Cell("C2").GetDouble().Should().BeApproximately(4500, 0.01, "display value preferred");
		ws.Cell("D2").Value.ToString().Should().Be("EUR");
		ws.Cell("H2").Value.ToString().Should().Be("FR76300040000312345");
	}

	[Fact]
	public async Task WriteAsync_UseDisplayValues_PicksDisplayBalance()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "s1", Balance = 10000m, DisplayBalance = 8000m, Name = "Livret" }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		wb.Worksheet("Savings").Cell("C2").GetDouble().Should().BeApproximately(8000, 0.01);
	}

	[Fact]
	public async Task WriteAsync_UseRawValues_PicksBalance()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "s1", Balance = 10000m, DisplayBalance = 8000m, Name = "Livret" }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = false }, CancellationToken.None);

		wb.Worksheet("Savings").Cell("C2").GetDouble().Should().BeApproximately(10000, 0.01);
	}

	[Fact]
	public async Task WriteAsync_NullFieldsHandledGracefully()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new()
				{
					Id = "c1", Name = null, Balance = null, DisplayBalance = null,
					BuyingValue = null, DisplayBuyingValue = null,
					UnrealizedPnl = null, AnnualYield = null,
					Iban = null, OpenedAt = null, LastSyncAt = null,
					Institution = null, Currency = null
				}
			]);

		using var wb = new XLWorkbook();
		var act = async () => await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task WriteAsync_CategoryErrorIsolation_ContinuesWithOtherCategories()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		// Checkings succeeds, Investments throws
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "c1", Name = "Checking", Balance = 100m }]);
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("Server error"));
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "s1", Name = "Savings", Balance = 200m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		// Both non-failing categories should have sheets
		wb.Worksheets.Should().Contain(ws => ws.Name == "Checkings");
		wb.Worksheets.Should().Contain(ws => ws.Name == "Savings");
	}

	[Fact]
	public async Task WriteAsync_AppliesCurrencyFormatWithSymbol()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "c1", Name = "Test", Balance = 100m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { DisplayCurrencySymbol = "€" }, CancellationToken.None);

		var ws = wb.Worksheet("Checkings");
		ws.Cell("C2").Style.NumberFormat.Format.Should().Contain("€");
	}

	[Fact]
	public async Task WriteAsync_AnnualYield_ConvertedToPercentage()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "s1", Name = "Livret A", AnnualYield = 3.0m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Savings");
		// 3.0% stored as 0.03 in the cell (3.0 / 100)
		ws.Cell("G2").GetDouble().Should().BeApproximately(0.03, 0.001);
	}

	[Fact]
	public async Task WriteAsync_UsesDisplayNameForSheetName()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.RealEstates, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "r1", Name = "Apartment" }]);
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.FondsEuro, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "f1", Name = "AV Fund" }]);
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.OtherAssets, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "o1", Name = "Art" }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		wb.Worksheets.Should().Contain(ws => ws.Name == "Real Estate");
		wb.Worksheets.Should().Contain(ws => ws.Name == "Fonds Euro");
		wb.Worksheets.Should().Contain(ws => ws.Name == "Other Assets");
	}

	[Fact]
	public async Task WriteAsync_IteratesAllTenCategories()
	{
		var mock = new Mock<IFinaryApiClient>();

		// Set up each of the 10 categories with one account
		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync([new() { Id = $"a-{cat}", Name = $"Account-{cat}" }]);
		}

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		wb.Worksheets.Should().HaveCount(10, "one sheet per category");
	}

	private static void SetupEmptyAccounts(Mock<IFinaryApiClient> mock)
	{
		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync([]);
		}
	}
}
