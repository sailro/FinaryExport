using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;

namespace FinaryExport.Tests.Export;

public sealed class HoldingsSheetTests
{
	[Fact]
	public async Task WriteAsync_WritesHeaderRow()
	{
		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		using var wb = new XLWorkbook();
		var sheet = new HoldingsSheet(NullLogger<HoldingsSheet>.Instance);
		await sheet.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Holdings");
		ws.Cell("A1").Value.ToString().Should().Be("Account");
		ws.Cell("B1").Value.ToString().Should().Be("Name");
		ws.Cell("C1").Value.ToString().Should().Be("ISIN");
		ws.Cell("D1").Value.ToString().Should().Be("Symbol");
		ws.Cell("E1").Value.ToString().Should().Be("Type");
		ws.Cell("F1").Value.ToString().Should().Be("Quantity");
		ws.Cell("G1").Value.ToString().Should().Be("Buy Price");
		ws.Cell("H1").Value.ToString().Should().Be("Current Price");
		ws.Cell("I1").Value.ToString().Should().Be("Value");
		ws.Cell("J1").Value.ToString().Should().Be("+/- Value");
		ws.Cell("K1").Value.ToString().Should().Be("+/- %");
	}

	[Fact]
	public async Task WriteAsync_WritesSecurityRows()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "pea-1", Name = "PEA Boursorama",
				Securities =
				[
					new()
					{
						Quantity = 42m,
						BuyingPrice = 100m,
						DisplayBuyingPrice = 95m,
						CurrentValue = 5000m,
						DisplayCurrentValue = 4750m,
						CurrentUpnl = 800m,
						DisplayCurrentUpnl = 760m,
						CurrentUpnlPercent = 1900m,
						DisplayCurrentUpnlPercent = 1800m,
						Security = new()
						{
							Name = "MSCI World ETF",
							Isin = "LU1681043599",
							Symbol = "CW8",
							SecurityType = "ETF",
							CurrentPrice = 119.05m
						}
					}
				]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		var sheet = new HoldingsSheet(NullLogger<HoldingsSheet>.Instance);
		// UseDisplayValues = true → prefer display values
		await sheet.WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Holdings");
		ws.Cell("A2").Value.ToString().Should().Be("PEA Boursorama");
		ws.Cell("B2").Value.ToString().Should().Be("MSCI World ETF");
		ws.Cell("C2").Value.ToString().Should().Be("LU1681043599");
		ws.Cell("D2").Value.ToString().Should().Be("CW8");
		ws.Cell("E2").Value.ToString().Should().Be("ETF");
		ws.Cell("F2").GetDouble().Should().Be(42);
		ws.Cell("G2").GetDouble().Should().Be(95);   // display value preferred
		ws.Cell("H2").GetDouble().Should().Be(119.05);
		ws.Cell("I2").GetDouble().Should().Be(4750);  // display value preferred
		ws.Cell("J2").GetDouble().Should().Be(760);   // display value preferred
	}

	[Fact]
	public async Task WriteAsync_SkipsAccountsWithoutSecurities()
	{
		var accounts = new List<Account>
		{
			new() { Id = "no-sec", Name = "Checking", Securities = null },
			new()
			{
				Id = "with-sec", Name = "PEA",
				Securities = [new() { Quantity = 1m, Security = new() { Name = "Stock A" } }]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		var sheet = new HoldingsSheet(NullLogger<HoldingsSheet>.Instance);
		await sheet.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Holdings");
		// Row 2 should be the only data row (from "PEA")
		ws.Cell("A2").Value.ToString().Should().Be("PEA");
		ws.Cell("A3").Value.ToString().Should().BeEmpty();
	}

	[Fact]
	public async Task WriteAsync_SortsByAccountThenSecurityName()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "z-account", Name = "Z Account",
				Securities =
				[
					new() { Security = new() { Name = "Beta" } },
					new() { Security = new() { Name = "Alpha" } }
				]
			},
			new()
			{
				Id = "a-account", Name = "A Account",
				Securities = [new() { Security = new() { Name = "Gamma" } }]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Investments, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new HoldingsSheet(NullLogger<HoldingsSheet>.Instance).WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Holdings");
		ws.Cell("A2").Value.ToString().Should().Be("A Account");
		ws.Cell("A3").Value.ToString().Should().Be("Z Account");
		ws.Cell("B3").Value.ToString().Should().Be("Alpha");
		ws.Cell("A4").Value.ToString().Should().Be("Z Account");
		ws.Cell("B4").Value.ToString().Should().Be("Beta");
	}

	[Fact]
	public void SheetName_IsHoldings()
	{
		new HoldingsSheet(NullLogger<HoldingsSheet>.Instance).SheetName.Should().Be("Holdings");
	}
}
