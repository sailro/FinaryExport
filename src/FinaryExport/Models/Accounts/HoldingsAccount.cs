namespace FinaryExport.Models.Accounts;

public sealed record HoldingsAccount
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
    public string? Name { get; init; }
    public string? ConnectionId { get; init; }
    public string? State { get; init; }
    public string? CorrelationId { get; init; }
    public decimal? Balance { get; init; }
    public decimal? DisplayBalance { get; init; }
    public decimal? OrganizationBalance { get; init; }
    public decimal? DisplayOrganizationBalance { get; init; }
    public decimal? BuyingValue { get; init; }
    public decimal? DisplayBuyingValue { get; init; }
    public string? LogoUrl { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
