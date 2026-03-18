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

public sealed class CryptoHoldingsSheetTests
{
	[Fact]
	public async Task WriteAsync_WritesHeaderRow()
	{
		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		using var wb = new XLWorkbook();
		var sheet = new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance);
		await sheet.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("A1").Value.ToString().Should().Be("Account");
		ws.Cell("B1").Value.ToString().Should().Be("Name");
		ws.Cell("C1").Value.ToString().Should().Be("Code");
		ws.Cell("D1").Value.ToString().Should().Be("Quantity");
		ws.Cell("E1").Value.ToString().Should().Be("Buy Price");
		ws.Cell("F1").Value.ToString().Should().Be("Current Price");
		ws.Cell("G1").Value.ToString().Should().Be("Value");
		ws.Cell("H1").Value.ToString().Should().Be("Buy Value");
		ws.Cell("I1").Value.ToString().Should().Be("+/- Value");
		ws.Cell("J1").Value.ToString().Should().Be("+/- %");
	}

	[Fact]
	public async Task WriteAsync_WritesCryptoRows()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "crypto-1", Name = "Binance",
				Cryptos =
				[
					new()
					{
						Quantity = 0.33456374m,
						BuyingPrice = 73805.02m,
						DisplayBuyingPrice = 70000.00m,
						CurrentPrice = 64282.99m,
						DisplayCurrentPrice = 61000.00m,
						CurrentValue = 21506.76m,
						DisplayCurrentValue = 20000.00m,
						BuyingValue = 24692.48m,
						DisplayBuyingValue = 23000.00m,
						UnrealizedPnl = -3185.73m,
						DisplayUnrealizedPnl = -3000.00m,
						UnrealizedPnlPercent = -12.90m,
						Crypto = new() { Name = "Bitcoin", Code = "BTC" }
					}
				]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		var sheet = new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance);
		await sheet.WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = true }, CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("A2").Value.ToString().Should().Be("Binance");
		ws.Cell("B2").Value.ToString().Should().Be("Bitcoin");
		ws.Cell("C2").Value.ToString().Should().Be("BTC");
		ws.Cell("D2").GetDouble().Should().Be(0.33456374);
		ws.Cell("E2").GetDouble().Should().Be(70000.00);   // display value preferred
		ws.Cell("F2").GetDouble().Should().Be(61000.00);   // display value preferred
		ws.Cell("G2").GetDouble().Should().Be(20000.00);   // display value preferred
		ws.Cell("H2").GetDouble().Should().Be(23000.00);   // display value preferred
		ws.Cell("I2").GetDouble().Should().Be(-3000.00);   // display value preferred
	}

	[Fact]
	public async Task WriteAsync_UsesRawValues_WhenDisplayValuesDisabled()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "c1", Name = "Kraken",
				Cryptos =
				[
					new()
					{
						Quantity = 1.0m,
						BuyingPrice = 50000.00m,
						DisplayBuyingPrice = 25000.00m,
						CurrentValue = 60000.00m,
						DisplayCurrentValue = 30000.00m,
						Crypto = new() { Name = "Bitcoin", Code = "BTC" }
					}
				]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		var sheet = new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance);
		await sheet.WriteAsync(wb, mock.Object, new ExportContext { UseDisplayValues = false }, CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("E2").GetDouble().Should().Be(50000.00);  // raw buying price
		ws.Cell("G2").GetDouble().Should().Be(60000.00);  // raw current value
	}

	[Fact]
	public async Task WriteAsync_SkipsAccountsWithoutCryptos()
	{
		var accounts = new List<Account>
		{
			new() { Id = "no-crypto", Name = "Checking", Cryptos = null },
			new()
			{
				Id = "with-crypto", Name = "Coinbase",
				Cryptos = [new() { Quantity = 5m, Crypto = new() { Name = "Ethereum", Code = "ETH" } }]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		var sheet = new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance);
		await sheet.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("A2").Value.ToString().Should().Be("Coinbase");
		ws.Cell("A3").Value.ToString().Should().BeEmpty();
	}

	[Fact]
	public async Task WriteAsync_SortsByAccountThenCryptoName()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "z-account", Name = "Z Exchange",
				Cryptos =
				[
					new() { Crypto = new() { Name = "Ethereum", Code = "ETH" } },
					new() { Crypto = new() { Name = "Bitcoin", Code = "BTC" } }
				]
			},
			new()
			{
				Id = "a-account", Name = "A Exchange",
				Cryptos = [new() { Crypto = new() { Name = "Solana", Code = "SOL" } }]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance)
			.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("A2").Value.ToString().Should().Be("A Exchange");
		ws.Cell("B2").Value.ToString().Should().Be("Solana");
		ws.Cell("A3").Value.ToString().Should().Be("Z Exchange");
		ws.Cell("B3").Value.ToString().Should().Be("Bitcoin");
		ws.Cell("A4").Value.ToString().Should().Be("Z Exchange");
		ws.Cell("B4").Value.ToString().Should().Be("Ethereum");
	}

	[Fact]
	public async Task WriteAsync_EmptyCryptosList_HeadersOnlyNoDataRows()
	{
		var accounts = new List<Account>
		{
			new() { Id = "empty", Name = "Empty Exchange", Cryptos = [] }
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance)
			.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("A1").Value.ToString().Should().Be("Account");
		ws.Cell("A2").Value.ToString().Should().BeEmpty();
	}

	[Fact]
	public async Task WriteAsync_NullCryptoInfo_HandlesGracefully()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "acct1", Name = "Mystery Exchange",
				Cryptos = [new() { Quantity = 100m, Crypto = null }]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance)
			.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("B2").Value.ToString().Should().BeEmpty();
		ws.Cell("C2").Value.ToString().Should().BeEmpty();
		ws.Cell("D2").GetDouble().Should().Be(100);
	}

	[Fact]
	public async Task WriteAsync_VerySmallQuantity_WrittenCorrectly()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "dust", Name = "DeFi Wallet",
				Cryptos =
				[
					new()
					{
						Quantity = 0.000000005958964247m,
						CurrentValue = 0.000000022644064m,
						Crypto = new() { Name = "Ethereum", Code = "ETH" }
					}
				]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance)
			.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		var qtyValue = ws.Cell("D2").GetDouble();
		qtyValue.Should().BeGreaterThan(0);
		qtyValue.Should().BeLessThan(0.0001);
	}

	[Fact]
	public async Task WriteAsync_PnlPercentDividedBy100()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "c1", Name = "Exchange",
				Cryptos =
				[
					new()
					{
						UnrealizedPnlPercent = -12.90m,
						Crypto = new() { Name = "Bitcoin", Code = "BTC" }
					}
				]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance)
			.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		// Sheet divides percent by 100 for Excel formatting
		ws.Cell("J2").GetDouble().Should().BeApproximately(-0.129, 0.0001);
	}

	[Fact]
	public async Task WriteAsync_ZeroQuantityCrypto_WritesRow()
	{
		var accounts = new List<Account>
		{
			new()
			{
				Id = "sold", Name = "Sold Out",
				Cryptos =
				[
					new()
					{
						Quantity = 0m,
						CurrentValue = 0m,
						BuyingValue = 5000m,
						Crypto = new() { Name = "Solana", Code = "SOL" }
					}
				]
			}
		};

		var mock = new Mock<IFinaryApiClient>();
		mock.Setup(x => x.GetCategoryAccountsAsync(AssetCategory.Cryptos, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(accounts);

		using var wb = new XLWorkbook();
		await new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance)
			.WriteAsync(wb, mock.Object, new ExportContext(), CancellationToken.None);

		var ws = wb.Worksheet("Crypto Holdings");
		ws.Cell("B2").Value.ToString().Should().Be("Solana");
		ws.Cell("D2").GetDouble().Should().Be(0);
	}

	[Fact]
	public void SheetName_IsCryptoHoldings()
	{
		new CryptoHoldingsSheet(NullLogger<CryptoHoldingsSheet>.Instance).SheetName.Should().Be("Crypto Holdings");
	}
}
