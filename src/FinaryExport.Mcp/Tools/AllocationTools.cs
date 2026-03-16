using System.ComponentModel;
using FinaryExport.Api;
using FinaryExport.Models.Portfolio;
using ModelContextProtocol.Server;

namespace FinaryExport.Mcp.Tools;

[McpServerToolType]
public class AllocationTools(IFinaryApiClient api)
{
	[McpServerTool(Name = "get_geographical_allocation"), Description("Get portfolio allocation breakdown by geographical region, showing how investments are distributed across countries and continents")]
	public async Task<AllocationData?> GetGeographicalAllocation(CancellationToken ct = default)
	{
		return await api.GetGeographicalAllocationAsync(ct);
	}

	[McpServerTool(Name = "get_sector_allocation"), Description("Get portfolio allocation breakdown by economic sector, showing distribution across industries like technology, finance, healthcare, etc.")]
	public async Task<AllocationData?> GetSectorAllocation(CancellationToken ct = default)
	{
		return await api.GetSectorAllocationAsync(ct);
	}
}
