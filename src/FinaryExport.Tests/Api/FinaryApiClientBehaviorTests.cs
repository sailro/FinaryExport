using System.Text.Json;
using FinaryExport.Api;
using FinaryExport.Models;
using FinaryExport.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Tests.Api;

public sealed class FinaryApiClientBehaviorTests
{
	[Fact]
	public async Task CategoryTimeseries_SendsRequiredAggregationParameters()
	{
		var handler = new MockHttpMessageHandler().EnqueueJson("""{"result": []}""");
		using var httpClient = CreateHttpClient(handler);
		var client = CreateApiClient(httpClient);
		client.SetOrganizationContext("org", "member");

		await client.GetCategoryTimeseriesAsync(AssetCategory.Checkings, "1m");

		var query = handler.SentRequests.Single().RequestUri!.Query;
		query.Should().Contain("period=1m");
		query.Should().Contain("timeseries_type=sum");
		query.Should().Contain("value_type=gross");
	}

	[Fact]
	public async Task BoundedTransactions_UsesOrganizationDatesAndFiltersResponse()
	{
		var today = DateTimeOffset.UtcNow;
		var handler = new MockHttpMessageHandler()
			.EnqueueJson("""{"result": [{"id":"account-1","name":"Current account"}]}""")
			.EnqueueJson(JsonSerializer.Serialize(new
			{
				result = new[]
				{
					Transaction(1, "account-1", today.AddDays(-2)),
					Transaction(2, "another-account", today.AddDays(-2)),
					Transaction(3, "account-1", today.AddMonths(-2)),
					Transaction(4, "account-1", today.AddDays(2))
				}
			}));
		using var httpClient = CreateHttpClient(handler);
		var client = CreateApiClient(httpClient);
		client.SetOrganizationContext("org", "member");

		var result = await client.GetCategoryTransactionsPageAsync(
			AssetCategory.Checkings,
			"1m",
			0,
			50);

		result.Should().ContainSingle().Which.Id.Should().Be(1);
		var transactionRequest = handler.SentRequests[1].RequestUri!;
		transactionRequest.AbsolutePath.Should().EndWith("/organizations/org/memberships/member/transactions");
		transactionRequest.Query.Should().Contain("account_id=account-1");
		transactionRequest.Query.Should().Contain("start_date=");
		transactionRequest.Query.Should().Contain("end_date=");
	}

	[Fact]
	public async Task PagedTransactions_ContinuesAfterSkippingInsideAFullPage()
	{
		var fullPage = Enumerable.Range(1, 200)
			.Select(id => Transaction(id, "account-1", DateTimeOffset.UtcNow.AddMinutes(-id)))
			.ToArray();
		var finalPage = Enumerable.Range(201, 2)
			.Select(id => Transaction(id, "account-1", DateTimeOffset.UtcNow.AddMinutes(-id)))
			.ToArray();
		var handler = new MockHttpMessageHandler()
			.EnqueueJson(JsonSerializer.Serialize(new { result = fullPage }))
			.EnqueueJson(JsonSerializer.Serialize(new { result = finalPage }));
		using var httpClient = CreateHttpClient(handler);
		var client = CreateApiClient(httpClient);
		client.SetOrganizationContext("org", "member");

		var result = await client.GetCategoryTransactionsPageAsync(
			AssetCategory.Checkings,
			"all",
			201,
			201);

		result.Should().HaveCount(201);
		handler.SentRequests.Should().HaveCount(2);
		handler.SentRequests[0].RequestUri!.Query.Should().Contain("page=2");
		handler.SentRequests[1].RequestUri!.Query.Should().Contain("page=3");
	}

	[Fact]
	public async Task ApiError_ExposesSafeProviderMessage()
	{
		var handler = new MockHttpMessageHandler().EnqueueJson(
			"""{"result":null,"error":{"code":"invalid_type","message":"Invalid type parameter"}}""",
			System.Net.HttpStatusCode.BadRequest);
		using var httpClient = CreateHttpClient(handler);
		var client = CreateApiClient(httpClient);
		client.SetOrganizationContext("org", "member");

		var act = () => client.GetCategoryTimeseriesAsync(AssetCategory.Checkings, "1m");

		await act.Should().ThrowAsync<HttpRequestException>()
			.WithMessage("*400*Invalid type parameter*");
	}

	private static object Transaction(int id, string accountId, DateTimeOffset date) => new
	{
		id,
		name = $"Transaction {id}",
		date = date.ToString("O"),
		value = id,
		account = new { id = accountId, name = "Current account" }
	};

	private static HttpClient CreateHttpClient(MockHttpMessageHandler handler) =>
		new(handler) { BaseAddress = new Uri(ApiBaseUrl) };

	private static FinaryApiClient CreateApiClient(HttpClient httpClient)
	{
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(item => item.CreateClient(ApiPaths.HttpClientName)).Returns(httpClient);
		return new FinaryApiClient(factory.Object, NullLogger<FinaryApiClient>.Instance);
	}
}
