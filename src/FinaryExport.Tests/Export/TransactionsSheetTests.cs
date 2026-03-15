using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Models;
using FinaryExport.Models.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinaryExport.Tests.Export;

public sealed class TransactionsSheetTests
{
	private static TransactionsSheet CreateSheet() => new(NullLogger<TransactionsSheet>.Instance);

	[Fact]
	public void SheetName_IsTransactions()
	{
		CreateSheet().SheetName.Should().Be("Transactions");
	}

	[Fact]
	public async Task WriteAsync_WritesHeaderRow()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Transactions");
		ws.Cell("A1").Value.ToString().Should().Be("Category");
		ws.Cell("B1").Value.ToString().Should().Be("Date");
		ws.Cell("C1").Value.ToString().Should().Be("Name");
		ws.Cell("D1").Value.ToString().Should().Be("Value");
		ws.Cell("E1").Value.ToString().Should().Be("Type");
		ws.Cell("F1").Value.ToString().Should().Be("Account");
		ws.Cell("G1").Value.ToString().Should().Be("Institution");
		ws.Cell("H1").Value.ToString().Should().Be("Native Currency");
		ws.Cell("I1").Value.ToString().Should().Be("Commission");
		ws.Cell("J1").Value.ToString().Should().Be("Transaction Category");
	}

	[Fact]
	public async Task WriteAsync_OnlyQueriesTransactionCapableCategories()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		// HasTransactions() returns true for: Checkings, Savings, Investments, Credits
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Credits, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

		// Non-transaction categories should NOT be queried
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.RealEstates, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.FondsEuro, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Commodities, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.OtherAssets, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
		mock.Verify(x => x.GetCategoryTransactionsAsync(AssetCategory.Startups, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task WriteAsync_WritesTransactionData()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new()
				{
					Id = 1001,
					Date = "2025-03-15",
					DisplayName = "Coffee Shop",
					Name = "coffee_shop",
					Value = -4.50m,
					DisplayValue = -4.50m,
					TransactionType = "expense",
					Commission = 0m,
					Category = new TransactionCategory { Name = "Food & Drink" },
					Currency = new TransactionCurrency { Code = "EUR" },
					Institution = new TransactionInstitution { Name = "BNP Paribas" },
					Account = new TransactionAccount { Name = "Main Checking" }
				}
			]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Transactions");
		ws.Cell("A2").Value.ToString().Should().Be("Checkings");
		ws.Cell("B2").Value.ToString().Should().Be("2025-03-15");
		ws.Cell("C2").Value.ToString().Should().Be("Coffee Shop");
		ws.Cell("D2").GetDouble().Should().BeApproximately(-4.50, 0.01);
		ws.Cell("E2").Value.ToString().Should().Be("expense");
		ws.Cell("F2").Value.ToString().Should().Be("Main Checking");
		ws.Cell("G2").Value.ToString().Should().Be("BNP Paribas");
		ws.Cell("H2").Value.ToString().Should().Be("EUR");
		ws.Cell("J2").Value.ToString().Should().Be("Food & Drink");
	}

	[Fact]
	public async Task WriteAsync_UsesDisplayValueWhenContextSaysDisplay()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new() { Id = 1, Value = 100m, DisplayValue = 80m }
			]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		wb.Worksheet("Transactions").Cell("D2").GetDouble().Should().BeApproximately(80, 0.01);
	}

	[Fact]
	public async Task WriteAsync_UsesRawValueWhenContextSaysRaw()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new() { Id = 1, Value = 100m, DisplayValue = 80m }
			]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = false }, CancellationToken.None);

		wb.Worksheet("Transactions").Cell("D2").GetDouble().Should().BeApproximately(100, 0.01);
	}

	[Fact]
	public async Task WriteAsync_EmptyTransactions_ShowsNoTransactionsMessage()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Transactions");
		ws.Cell("A2").Value.ToString().Should().Be("No transactions found");
	}

	[Fact]
	public async Task WriteAsync_MultipleCategories_AggregatesAllTransactions()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = 1, Name = "Checking TX", Value = 100m }]);
		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = 2, Name = "Savings TX", Value = 200m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Transactions");
		ws.Cell("A2").Value.ToString().Should().Be("Checkings");
		ws.Cell("A3").Value.ToString().Should().Be("Savings");
		ws.Cell("C2").Value.ToString().Should().Be("Checking TX");
		ws.Cell("C3").Value.ToString().Should().Be("Savings TX");
	}

	[Fact]
	public async Task WriteAsync_CategoryFailure_ContinuesWithOtherCategories()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		// Checkings throws, Savings succeeds
		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("API error: 500"));
		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = 1, Name = "Savings TX", Value = 50m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Transactions");
		// Savings should still be written even though Checkings failed
		ws.Cell("A2").Value.ToString().Should().Be("Savings");
		ws.Cell("C2").Value.ToString().Should().Be("Savings TX");
	}

	[Fact]
	public async Task WriteAsync_NullFields_HandledGracefully()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new()
				{
					Id = 1,
					Date = null, DisplayName = null, Name = null,
					Value = null, DisplayValue = null,
					TransactionType = null, Commission = null,
					Category = null, Currency = null,
					Institution = null, Account = null
				}
			]);

		using var wb = new XLWorkbook();
		var act = async () => await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task WriteAsync_AppliesCurrencyFormat()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = 1, Value = 50m, Commission = 1.5m }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { DisplayCurrencySymbol = "€" }, CancellationToken.None);

		var ws = wb.Worksheet("Transactions");
		ws.Cell("D2").Style.NumberFormat.Format.Should().Contain("€");
		ws.Cell("I2").Style.NumberFormat.Format.Should().Contain("€");
	}

	[Fact]
	public async Task WriteAsync_FallsBackToNameWhenDisplayNameNull()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyTransactions(mock);

		mock.Setup(x => x.GetCategoryTransactionsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = 1, DisplayName = null, Name = "raw_name" }]);

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		wb.Worksheet("Transactions").Cell("C2").Value.ToString().Should().Be("raw_name");
	}

	private static void SetupEmptyTransactions(Mock<IFinaryApiClient> mock)
	{
		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			mock.Setup(x => x.GetCategoryTransactionsAsync(cat, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync([]);
		}
	}
}
