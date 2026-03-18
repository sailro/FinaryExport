using System.Text.Json;
using FinaryExport.Models.Accounts;
using FluentAssertions;

namespace FinaryExport.Tests.Api;

public sealed class CryptoDeserializationTests
{
	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		PropertyNameCaseInsensitive = true
	};

	[Fact]
	public void Deserialize_CryptoPosition_AllFieldsMapped()
	{
		var json = """
		{
			"correlation_id": "BTC",
			"quantity": 0.33456374,
			"buying_price": 73805.02,
			"current_price": 64282.99,
			"current_value": 21506.76,
			"buying_value": 24692.48,
			"unrealized_pnl": -3185.73,
			"unrealized_pnl_percent": -12.90,
			"crypto": { "name": "Bitcoin", "code": "BTC" }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.CorrelationId.Should().Be("BTC");
		pos.Quantity.Should().Be(0.33456374m);
		pos.BuyingPrice.Should().Be(73805.02m);
		pos.CurrentPrice.Should().Be(64282.99m);
		pos.CurrentValue.Should().Be(21506.76m);
		pos.BuyingValue.Should().Be(24692.48m);
		pos.UnrealizedPnl.Should().Be(-3185.73m);
		pos.UnrealizedPnlPercent.Should().Be(-12.90m);
	}

	[Fact]
	public void Deserialize_CryptoPosition_CryptoInfoMapped()
	{
		var json = """
		{
			"correlation_id": "ETH",
			"quantity": 2.5,
			"crypto": { "name": "Ethereum", "code": "ETH", "symbol": "Ξ", "logo_url": "https://cdn.example.com/eth.png", "id": 42 }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.Crypto.Should().NotBeNull();
		pos.Crypto!.Name.Should().Be("Ethereum");
		pos.Crypto.Code.Should().Be("ETH");
		pos.Crypto.Symbol.Should().Be("Ξ");
		pos.Crypto.LogoUrl.Should().Be("https://cdn.example.com/eth.png");
		pos.Crypto.Id.Should().Be(42);
	}

	[Fact]
	public void Deserialize_AccountWithCryptos_PopulatesList()
	{
		var json = """
		{
			"id": "acct_crypto_1",
			"name": "Binance",
			"balance": 50000.00,
			"cryptos": [
				{
					"correlation_id": "BTC",
					"quantity": 0.5,
					"buying_price": 60000.00,
					"current_price": 64000.00,
					"current_value": 32000.00,
					"buying_value": 30000.00,
					"unrealized_pnl": 2000.00,
					"unrealized_pnl_percent": 6.67,
					"crypto": { "name": "Bitcoin", "code": "BTC" }
				},
				{
					"correlation_id": "ETH",
					"quantity": 10.0,
					"buying_price": 3000.00,
					"current_price": 3500.00,
					"current_value": 35000.00,
					"buying_value": 30000.00,
					"unrealized_pnl": 5000.00,
					"unrealized_pnl_percent": 16.67,
					"crypto": { "name": "Ethereum", "code": "ETH" }
				}
			]
		}
		""";

		var account = JsonSerializer.Deserialize<Account>(json, _jsonOptions)!;

		account.Id.Should().Be("acct_crypto_1");
		account.Name.Should().Be("Binance");
		account.Cryptos.Should().NotBeNull();
		account.Cryptos.Should().HaveCount(2);
		account.Cryptos![0].CorrelationId.Should().Be("BTC");
		account.Cryptos[0].Crypto!.Name.Should().Be("Bitcoin");
		account.Cryptos[1].CorrelationId.Should().Be("ETH");
		account.Cryptos[1].Crypto!.Name.Should().Be("Ethereum");
	}

	[Fact]
	public void Deserialize_AccountWithEmptyCryptos_EmptyList()
	{
		var json = """
		{
			"id": "acct_empty",
			"name": "Empty Wallet",
			"cryptos": []
		}
		""";

		var account = JsonSerializer.Deserialize<Account>(json, _jsonOptions)!;

		account.Cryptos.Should().NotBeNull();
		account.Cryptos.Should().BeEmpty();
	}

	[Fact]
	public void Deserialize_AccountWithNoCryptosField_NullList()
	{
		var json = """
		{
			"id": "acct_checking",
			"name": "Checking Account",
			"balance": 1500.00
		}
		""";

		var account = JsonSerializer.Deserialize<Account>(json, _jsonOptions)!;

		account.Cryptos.Should().BeNull();
	}

	[Fact]
	public void Deserialize_CryptoWithNullQuantity_NullValue()
	{
		var json = """
		{
			"correlation_id": "DOGE",
			"quantity": null,
			"crypto": { "name": "Dogecoin", "code": "DOGE" }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.Quantity.Should().BeNull();
		pos.Crypto!.Name.Should().Be("Dogecoin");
	}

	[Fact]
	public void Deserialize_CryptoWithZeroQuantity_ZeroValue()
	{
		var json = """
		{
			"correlation_id": "SOL",
			"quantity": 0,
			"buying_price": 150.00,
			"current_price": 180.00,
			"current_value": 0,
			"crypto": { "name": "Solana", "code": "SOL" }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.Quantity.Should().Be(0m);
		pos.CurrentValue.Should().Be(0m);
	}

	[Fact]
	public void Deserialize_CryptoWithVerySmallQuantity_PreservesPrecision()
	{
		// Real scenario: ETH dust amounts from DeFi operations
		var json = """
		{
			"correlation_id": "ETH",
			"quantity": 5.958964247e-9,
			"buying_price": 3500.00,
			"current_price": 3800.00,
			"current_value": 0.000000022644064,
			"crypto": { "name": "Ethereum", "code": "ETH" }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.Quantity.Should().BeGreaterThan(0m);
		pos.Quantity.Should().BeLessThan(0.0001m);
		// Verify we don't lose the value entirely due to precision
		pos.Quantity!.Value.Should().BeApproximately(5.958964247E-9m, 1E-15m);
	}

	[Fact]
	public void Deserialize_CryptoWithNullCryptoInfo_NullObject()
	{
		var json = """
		{
			"correlation_id": "UNKNOWN",
			"quantity": 100.0,
			"crypto": null
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.CorrelationId.Should().Be("UNKNOWN");
		pos.Quantity.Should().Be(100.0m);
		pos.Crypto.Should().BeNull();
	}

	[Fact]
	public void Deserialize_CryptoWithDisplayValues_MappedCorrectly()
	{
		var json = """
		{
			"correlation_id": "BTC",
			"quantity": 1.0,
			"buying_price": 50000.00,
			"display_buying_price": 25000.00,
			"buying_value": 50000.00,
			"display_buying_value": 25000.00,
			"current_price": 60000.00,
			"display_current_price": 30000.00,
			"current_value": 60000.00,
			"display_current_value": 30000.00,
			"unrealized_pnl": 10000.00,
			"display_unrealized_pnl": 5000.00,
			"crypto": { "name": "Bitcoin", "code": "BTC" }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		// Raw values
		pos.BuyingPrice.Should().Be(50000.00m);
		pos.BuyingValue.Should().Be(50000.00m);
		pos.CurrentPrice.Should().Be(60000.00m);
		pos.CurrentValue.Should().Be(60000.00m);
		pos.UnrealizedPnl.Should().Be(10000.00m);

		// Display (ownership-adjusted) values
		pos.DisplayBuyingPrice.Should().Be(25000.00m);
		pos.DisplayBuyingValue.Should().Be(25000.00m);
		pos.DisplayCurrentPrice.Should().Be(30000.00m);
		pos.DisplayCurrentValue.Should().Be(30000.00m);
		pos.DisplayUnrealizedPnl.Should().Be(5000.00m);
	}

	[Fact]
	public void Deserialize_CryptoWithNegativePnl_PreservesSign()
	{
		var json = """
		{
			"correlation_id": "LUNA",
			"quantity": 1000.0,
			"buying_price": 100.00,
			"current_price": 0.0001,
			"current_value": 0.10,
			"buying_value": 100000.00,
			"unrealized_pnl": -99999.90,
			"unrealized_pnl_percent": -99.9999,
			"crypto": { "name": "Terra Classic", "code": "LUNA" }
		}
		""";

		var pos = JsonSerializer.Deserialize<CurrencyPosition>(json, _jsonOptions)!;

		pos.UnrealizedPnl.Should().BeNegative();
		pos.UnrealizedPnlPercent.Should().BeLessThan(-99m);
	}

	[Fact]
	public void Deserialize_AccountCryptosWrappedInFinaryResponse_Works()
	{
		// Tests the full API envelope + account + nested cryptos
		var json = """
		{
			"result": [
				{
					"id": "acct_1",
					"name": "Kraken",
					"cryptos": [
						{
							"correlation_id": "BTC",
							"quantity": 0.1,
							"crypto": { "name": "Bitcoin", "code": "BTC" }
						}
					]
				}
			],
			"message": null,
			"error": null
		}
		""";

		using var doc = JsonDocument.Parse(json);
		var resultElement = doc.RootElement.GetProperty("result");
		var accounts = JsonSerializer.Deserialize<List<Account>>(resultElement.GetRawText(), _jsonOptions)!;

		accounts.Should().HaveCount(1);
		accounts[0].Cryptos.Should().HaveCount(1);
		accounts[0].Cryptos![0].Crypto!.Code.Should().Be("BTC");
	}
}
