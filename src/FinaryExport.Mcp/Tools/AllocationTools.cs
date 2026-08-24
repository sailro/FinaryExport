using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class AllocationTools(IMcpFinaryApiClientFactory clients)
{
	[McpServerTool(Name = "get_geographical_allocation", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get a concise active-profile geographical allocation without internal contribution metadata")]
	public async Task<AllocationResponse> GetGeographicalAllocation(CancellationToken ct = default)
	{
		var api = await clients.CreateActiveClientAsync(ct);
		var allocation = await api.GetGeographicalAllocationAsync(ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Allocation(allocation, currency);
	}

	[McpServerTool(Name = "get_sector_allocation", UseStructuredContent = true, ReadOnly = true, Idempotent = true), Description("Get a concise active-profile sector allocation without internal contribution metadata")]
	public async Task<AllocationResponse> GetSectorAllocation(CancellationToken ct = default)
	{
		var api = await clients.CreateActiveClientAsync(ct);
		var allocation = await api.GetSectorAllocationAsync(ct);
		var currency = McpMapper.DisplayCurrency(await api.GetCurrentUserAsync(ct));
		return McpMapper.Allocation(allocation, currency);
	}
}
