using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using ModelContextProtocol.Server;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class PortfolioTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_portfolio_summary", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get authoritative active-profile totals: gross assets, liabilities, net worth, financial assets, category allocation, and performance. Never recompute these totals from account rows.")]
	public async Task<PortfolioResponse> GetPortfolioSummary(
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period = Defaults.DefaultPeriod,
		CancellationToken ct = default)
	{
		period = ToolInputs.Period(period);
		var api = await clients.CreateActiveClientAsync(ct);
		var portfolio = await api.GetPortfolioAsync(period, ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Portfolio(portfolio, period, currency);
	}

	[McpServerTool(Name = "get_portfolio_timeseries", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get a paged historical active-profile portfolio series as normalized date/value points")]
	public async Task<TimeseriesResponse> GetPortfolioTimeseries(
		[Description("Time period. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y"), AllowedValues("all", "1d", "1w", "1m", "3m", "6m", "1y", "5y")] string period,
		[Description("Value type to chart. Options: gross, net. Default: gross"), AllowedValues("gross", "net")] string valueType = Defaults.DefaultValueType,
		[Description("Zero-based point offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum points to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		period = ToolInputs.Period(period);
		valueType = ToolInputs.ValueType(valueType);
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var series = await api.GetPortfolioTimeseriesAsync(period, valueType, ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Timeseries(series, period, currency, offset, limit);
	}

	[McpServerTool(Name = "get_portfolio_fees", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get concise active-profile fee totals and contract-level potential savings")]
	public async Task<FeesResponse> GetPortfolioFees(CancellationToken ct = default)
	{
		var api = await clients.CreateActiveClientAsync(ct);
		var fees = await api.GetPortfolioFeesAsync(ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Fees(fees, currency);
	}
}
