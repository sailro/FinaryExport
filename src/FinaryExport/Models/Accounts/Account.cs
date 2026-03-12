namespace FinaryExport.Models.Accounts;

public sealed record Account
{
    public string? Slug { get; init; }
    public string? Name { get; init; }
    public string? ConnectionId { get; init; }
    public string? State { get; init; }
    public string? StateMessage { get; init; }
    public string? CorrelationId { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
    public DateTimeOffset? OpenedAt { get; init; }
    public string? Id { get; init; }
    public string? ManualType { get; init; }
    public string? LogoUrl { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public decimal? AnnualYield { get; init; }
    public decimal? Balance { get; init; }
    public decimal? DisplayBalance { get; init; }
    public decimal? OrganizationBalance { get; init; }
    public decimal? DisplayOrganizationBalance { get; init; }
    public decimal? BuyingValue { get; init; }
    public decimal? DisplayBuyingValue { get; init; }
    public decimal? UnrealizedPnl { get; init; }
    public decimal? Share { get; init; }
    public bool IsManual { get; init; }
    public string? Category { get; init; }
    public DateTimeOffset? LastSyncAt { get; init; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; init; }
    public AccountInstitution? Institution { get; init; }
    public AccountCurrency? Currency { get; init; }
    public AccountBankAccountType? BankAccountType { get; init; }
}

public sealed record AccountInstitution
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
    public string? Name { get; init; }
}

public sealed record AccountCurrency
{
    public string? Code { get; init; }
    public string? Symbol { get; init; }
    public string? Name { get; init; }
}

public sealed record AccountBankAccountType
{
    public string? Slug { get; init; }
    public string? DisplayName { get; init; }
    public string? AccountType { get; init; }
}
