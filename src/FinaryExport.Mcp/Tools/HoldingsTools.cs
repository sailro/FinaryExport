using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class HoldingsTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_holdings"), Description("Get all holdings accounts with their security positions, balances, and transaction details")]
	public async Task<List<HoldingsAccount>> GetHoldings(CancellationToken ct = default)
	{
		return await api.GetHoldingsAccountsAsync(ct);
	}

	[McpServerTool(Name = "get_asset_list"), Description("Get the flat list of all individual holdings and positions across all accounts, with current value, unrealized profit/loss, and performance")]
	public async Task<List<AssetListEntry>> GetAssetList(
		[Description("Time period filter. Options: all, 1d, 1w, 1m, 3m, 6m, 1y, 5y. Default: all")] string period = "all",
		CancellationToken ct = default)
	{
		return await api.GetAssetListAsync(period, ct);
	}
}
