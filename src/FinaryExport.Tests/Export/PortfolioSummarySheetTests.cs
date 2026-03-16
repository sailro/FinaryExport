using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Models;
using FinaryExport.Models.Portfolio;
using FluentAssertions;
using Moq;

namespace FinaryExport.Tests.Export;

public sealed class PortfolioSummarySheetTests
{
	private static PortfolioSummarySheet CreateSheet() => new();

	[Fact]
	public void SheetName_IsSummary()
	{
		CreateSheet().SheetName.Should().Be("Summary");
	}

	[Fact]
	public async Task WriteAsync_WritesGrossAndNetSummary()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new PortfolioSummary
			{
				Gross = new PortfolioValues
				{
					Total = new PortfolioTotalValues
					{
						Amount = 150000m, DisplayAmount = 148000m,
						Evolution = 5000m, EvolutionPercent = 3.5m
					}
				},
				Net = new PortfolioValues
				{
					Total = new PortfolioTotalValues
					{
						Amount = 140000m, DisplayAmount = 138000m,
						Evolution = 4500m, EvolutionPercent = 3.3m
					}
				}
			});

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Summary");
		ws.Cell("A1").Value.ToString().Should().Be("Portfolio Summary");
		ws.Cell("A4").Value.ToString().Should().Be("Gross Total");
		ws.Cell("B4").GetDouble().Should().BeApproximately(148000, 0.01, "display value");
		ws.Cell("A5").Value.ToString().Should().Be("Gross Evolution");
		ws.Cell("B5").GetDouble().Should().BeApproximately(5000, 0.01);
		ws.Cell("A6").Value.ToString().Should().Be("Gross Evolution %");
		ws.Cell("B6").GetDouble().Should().BeApproximately(0.035, 0.001, "3.5 / 100");
	}

	[Fact]
	public async Task WriteAsync_WritesCategoryBreakdown()
	{
		var mock = new Mock<IFinaryApiClient>();

		mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new PortfolioSummary());

		// Checkings: 2 accounts totaling 5000
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Checkings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new() { Id = "c1", Balance = 3000m },
				new() { Id = "c2", Balance = 2000m }
			]);

		// All others empty
		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			if (cat == AssetCategory.Checkings) continue;
			mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync([]);
		}

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Summary");

		// Find the category breakdown section
		var found = false;
		for (var row = 1; row <= 30; row++)
		{
			if (ws.Cell($"A{row}").Value.ToString() == "Checkings")
			{
				found = true;
				ws.Cell($"B{row}").GetDouble().Should().Be(2, "2 accounts");
				ws.Cell($"C{row}").GetDouble().Should().BeApproximately(5000, 0.01, "3000 + 2000");
				break;
			}
		}
		found.Should().BeTrue("Checkings category should appear in breakdown");
	}

	[Fact]
	public async Task WriteAsync_NullPortfolio_DoesNotThrow()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((PortfolioSummary?)null);

		using var wb = new XLWorkbook();
		var act = async () => await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task WriteAsync_CategoryError_ShowsErrorInBreakdown()
	{
		var mock = new Mock<IFinaryApiClient>();

		mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new PortfolioSummary());

		// Investments throws
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("API error"));

		// Others empty
		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			if (cat == AssetCategory.Investments) continue;
			mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync([]);
		}

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Summary");

		// Find the Investments row — should show "Error"
		var found = false;
		for (var row = 1; row <= 30; row++)
		{
			if (ws.Cell($"A{row}").Value.ToString() == "Investments")
			{
				found = true;
				ws.Cell($"B{row}").Value.ToString().Should().Be("Error");
				break;
			}
		}
		found.Should().BeTrue("Investments category should still appear with error");
	}

	[Fact]
	public async Task WriteAsync_UsesDisplayBalanceForCategoryTotals()
	{
		var mock = new Mock<IFinaryApiClient>();

		mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new PortfolioSummary());

		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Savings, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new() { Id = "s1", Balance = 10000m, DisplayBalance = 8000m }]);

		foreach (var cat in Enum.GetValues<AssetCategory>())
		{
			if (cat == AssetCategory.Savings) continue;
			mock.Setup(x => x.GetCategoryAccountsAsync(cat, It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync([]);
		}

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Summary");

		for (var row = 1; row <= 30; row++)
		{
			if (ws.Cell($"A{row}").Value.ToString() == "Savings")
			{
				ws.Cell($"C{row}").GetDouble().Should().BeApproximately(8000, 0.01, "display value preferred");
				return;
			}
		}

		Assert.Fail("Savings category not found in breakdown");
	}

	[Fact]
	public async Task WriteAsync_AppliesCurrencyFormat()
	{
		var mock = new Mock<IFinaryApiClient>();
		SetupEmptyAccounts(mock);

		mock.Setup(x => x.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new PortfolioSummary
			{
				Gross = new PortfolioValues
				{
					Total = new PortfolioTotalValues { Amount = 100000m, DisplayAmount = 100000m }
				}
			});

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { DisplayCurrencySymbol = "£" }, CancellationToken.None);

		wb.Worksheet("Summary").Cell("B4").Style.NumberFormat.Format.Should().Contain("£");
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
