namespace FinaryExport.Models.Accounts;

public sealed record AssetInfo
{
	public long? Id { get; init; }
	public string? Name { get; init; }
	public string? Code { get; init; }
	public string? Symbol { get; init; }
	public string? LogoUrl { get; init; }
}
