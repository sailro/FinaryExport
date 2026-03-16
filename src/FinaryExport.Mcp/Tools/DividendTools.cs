using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models.Portfolio;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class DividendTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_dividends"), Description("Get dividend income summary including annual income, yield, past dividends, upcoming dividends, and per-asset-type breakdowns")]
	public async Task<DividendSummary?> GetDividends(CancellationToken ct = default)
	{
		return await api.GetPortfolioDividendsAsync(ct);
	}
}
