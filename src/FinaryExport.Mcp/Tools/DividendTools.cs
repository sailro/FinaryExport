using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class DividendTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_dividends", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get concise active-profile dividend totals and a bounded page of past/upcoming events")]
	public async Task<DividendsResponse> GetDividends(
		[Description("Zero-based dividend-event offset. Default: 0"), Range(0, int.MaxValue)] int offset = 0,
		[Description("Maximum dividend events to return, from 1 to 500. Default: 100"), Range(1, ToolInputs.MaximumPageSize)] int limit = ToolInputs.DefaultPageSize,
		CancellationToken ct = default)
	{
		(offset, limit) = ToolInputs.Page(offset, limit);
		var api = await clients.CreateActiveClientAsync(ct);
		var dividends = await api.GetPortfolioDividendsAsync(ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Dividends(dividends, currency, offset, limit);
	}
}
