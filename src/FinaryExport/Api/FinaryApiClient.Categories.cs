using FinaryExport.Models;
using FinaryExport.Models.Accounts;
using FinaryExport.Models.Portfolio;

namespace FinaryExport.Api;

public sealed partial class FinaryApiClient
{
    public async Task<List<Account>> GetCategoryAccountsAsync(
        AssetCategory category, CancellationToken ct = default)
    {
        return await GetAsync<List<Account>>(
            $"{BasePath}/portfolio/{category.ToUrlSegment()}/accounts?period=1d", ct) ?? [];
    }

    public async Task<List<TimeseriesData>> GetCategoryTimeseriesAsync(
        AssetCategory category, string period = "all", CancellationToken ct = default)
    {
        return await GetAsync<List<TimeseriesData>>(
            $"{BasePath}/portfolio/{category.ToUrlSegment()}/timeseries?new_format=true&period={period}", ct)
            ?? [];
    }
}
