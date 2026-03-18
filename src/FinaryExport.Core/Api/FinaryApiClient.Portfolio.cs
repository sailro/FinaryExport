using FinaryExport.Models.Portfolio;

using static FinaryExport.FinaryConstants;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
	public async Task<PortfolioSummary?> GetPortfolioAsync(string period = Defaults.DefaultPeriod, CancellationToken ct = default)
	{
		return await GetAsync<PortfolioSummary>($"{BasePath}/portfolio?new_format=true&period={period}", ct);
	}

	public async Task<List<TimeseriesData>> GetPortfolioTimeseriesAsync(string period, string valueType = Defaults.DefaultValueType, CancellationToken ct = default)
	{
		return await GetAsync<List<TimeseriesData>>($"{BasePath}/portfolio/timeseries?new_format=true&period={period}&timeseries_type=sum&value_type={valueType}", ct)
			?? [];
	}

	public async Task<DividendSummary?> GetPortfolioDividendsAsync(CancellationToken ct = default)
	{
		return await GetAsync<DividendSummary>($"{BasePath}/portfolio/dividends?with_real_estate=true", ct);
	}

	public async Task<AllocationData?> GetGeographicalAllocationAsync(CancellationToken ct = default)
	{
		return await GetAsync<AllocationData>($"{BasePath}/portfolio/geographical_allocation", ct);
	}

	public async Task<AllocationData?> GetSectorAllocationAsync(CancellationToken ct = default)
	{
		return await GetAsync<AllocationData>($"{BasePath}/portfolio/sector_allocation", ct);
	}

	public async Task<FeeSummary?> GetPortfolioFeesAsync(CancellationToken ct = default)
	{
		return await GetAsync<FeeSummary>($"{BasePath}/portfolio/fees?new_format=true", ct);
	}
}
