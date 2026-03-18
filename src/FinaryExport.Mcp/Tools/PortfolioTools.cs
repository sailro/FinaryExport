using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models.Portfolio;
using ModelContextProtocol.Server;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class PortfolioTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_portfolio_summary"), Description("Get the total portfolio valuation including gross and net values, evolution, and period performance")]
	public async Task<PortfolioSummary?> GetPortfolioSummary(
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = Defaults.DefaultPeriod,
		CancellationToken ct = default)
	{
		return await api.GetPortfolioAsync(period, ct);
	}

	[McpServerTool(Name = "get_portfolio_timeseries"), Description("Get historical portfolio value over time as date/value pairs, useful for charting and trend analysis")]
	public async Task<List<TimeseriesData>> GetPortfolioTimeseries(
		[Description("Time period. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y")] string period,
		[Description("Value type to chart. Options: gross, net. Default: gross")] string valueType = Defaults.DefaultValueType,
		CancellationToken ct = default)
	{
		return await api.GetPortfolioTimeseriesAsync(period, valueType, ct);
	}

	[McpServerTool(Name = "get_portfolio_fees"), Description("Get fee analysis including annual fees, cumulated fees, potential savings, and fee timeseries")]
	public async Task<FeeSummary?> GetPortfolioFees(CancellationToken ct = default)
	{
		return await api.GetPortfolioFeesAsync(ct);
	}
}
