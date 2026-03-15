using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Models.Portfolio;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinaryExport.Tests.Export;

public sealed class DividendsSheetTests
{
	private static DividendsSheet CreateSheet() => new(NullLogger<DividendsSheet>.Instance);

	[Fact]
	public void SheetName_IsDividends()
	{
		CreateSheet().SheetName.Should().Be("Dividends");
	}

	[Fact]
	public async Task WriteAsync_WritesSummarySection()
	{
		var mock = SetupDividends(new DividendSummary
		{
			AnnualIncome = 3500m,
			PastIncome = 2800m,
			NextYear = [new() { Date = "2026-04-01", Value = 925m }, new() { Date = "2026-07-01", Value = 925m }],
			Yield = 2.33m
		});

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Dividends");
		ws.Cell("A1").Value.ToString().Should().Be("Dividends Summary");
		ws.Cell("A4").Value.ToString().Should().Be("Annual Income");
		ws.Cell("B4").GetDouble().Should().BeApproximately(3500, 0.01);
		ws.Cell("A5").Value.ToString().Should().Be("Past Income");
		ws.Cell("B5").GetDouble().Should().BeApproximately(2800, 0.01);
		ws.Cell("A6").Value.ToString().Should().Be("Next Year");
		ws.Cell("B6").GetDouble().Should().BeApproximately(1850, 0.01, "925 + 925");
		ws.Cell("A7").Value.ToString().Should().Be("Yield");
		ws.Cell("B7").GetDouble().Should().BeApproximately(0.0233, 0.0001, "2.33 / 100");
	}

	[Fact]
	public async Task WriteAsync_WritesPastDividendsDetail()
	{
		var mock = SetupDividends(new DividendSummary
		{
			AnnualIncome = 1000m,
			PastDividends =
			[
				new()
				{
					Id = 1, Amount = 200m, DisplayAmount = 180m,
					PaymentAt = "2025-06-15", AssetSubtype = "SCPI", AssetType = "RealEstate",
					Asset = new DividendAssetInfo { Name = "SCPI Primovie" }
				}
			]
		});

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Dividends");
		ws.Cell("A10").Value.ToString().Should().Be("Past Dividends");
		ws.Cell("A12").Value.ToString().Should().Be("SCPI Primovie");
		ws.Cell("B12").GetDouble().Should().BeApproximately(180, 0.01, "display value");
		ws.Cell("C12").Value.ToString().Should().Be("2025-06-15");
		ws.Cell("D12").Value.ToString().Should().Be("SCPI");
		ws.Cell("E12").Value.ToString().Should().Be("RealEstate");
	}

	[Fact]
	public async Task WriteAsync_WritesUpcomingDividendsDetail()
	{
		var mock = SetupDividends(new DividendSummary
		{
			AnnualIncome = 500m,
			UpcomingDividends =
			[
				new()
				{
					Id = 2, Amount = 100m,
					PaymentAt = "2026-03-15", Status = "projected",
					AssetType = "ETF",
					Holding = new DividendAssetInfo { Name = "MSCI World" }
				}
			]
		});

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Dividends");
		// Upcoming section starts after past dividends gap
		var found = false;
		for (var row = 1; row <= 30; row++)
		{
			if (ws.Cell($"A{row}").Value.ToString() == "Upcoming Dividends")
			{
				found = true;
				// Header row is row+1, data row is row+2
				ws.Cell($"A{row + 2}").Value.ToString().Should().Be("MSCI World");
				ws.Cell($"D{row + 2}").Value.ToString().Should().Be("projected");
				break;
			}
		}
		found.Should().BeTrue("Upcoming Dividends section should exist");
	}

	[Fact]
	public async Task WriteAsync_NullDividends_WritesZeros()
	{
		var mock = SetupDividends(null);

		using var wb = new XLWorkbook();
		var act = async () => await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		await act.Should().NotThrowAsync();

		var ws = wb.Worksheet("Dividends");
		ws.Cell("B4").GetDouble().Should().Be(0, "null AnnualIncome defaults to 0");
		ws.Cell("B5").GetDouble().Should().Be(0, "null PastIncome defaults to 0");
	}

	[Fact]
	public async Task WriteAsync_DividendNameFallback_UsesHoldingThenAssetType()
	{
		var mock = SetupDividends(new DividendSummary
		{
			PastDividends =
			[
				new() { Id = 1, Asset = null, Holding = new DividendAssetInfo { Name = "Holding Name" }, AssetType = "Bond" },
				new() { Id = 2, Asset = null, Holding = null, AssetType = "SCPI" }
			]
		});

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Dividends");
		ws.Cell("A12").Value.ToString().Should().Be("Holding Name");
		ws.Cell("A13").Value.ToString().Should().Be("SCPI");
	}

	[Fact]
	public async Task WriteAsync_AppliesCurrencyFormat()
	{
		var mock = SetupDividends(new DividendSummary { AnnualIncome = 1000m });

		using var wb = new XLWorkbook();
		await CreateSheet().WriteAsync(wb, mock.Object, new ExportContext { DisplayCurrencySymbol = "$" }, CancellationToken.None);

		var ws = wb.Worksheet("Dividends");
		ws.Cell("B4").Style.NumberFormat.Format.Should().Contain("$");
	}

	private static Mock<IFinaryApiClient> SetupDividends(DividendSummary? dividends)
	{
		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetPortfolioDividendsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(dividends);
		return mock;
	}
}
