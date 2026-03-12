using System.Text.Json.Serialization;

namespace FinaryExport.Models.Transactions;

public sealed record Transaction
{
    public long? Id { get; init; }
    public string? Name { get; init; }
    public string? SimplifiedName { get; init; }
    public string? DisplayName { get; init; }
    public string? CorrelationId { get; init; }
    public string? Date { get; init; }
    public string? DisplayDate { get; init; }
    public decimal? Value { get; init; }
    public decimal? DisplayValue { get; init; }
    public string? TransactionType { get; init; }
    public decimal? Commission { get; init; }
    public int? ExternalIdCategory { get; init; }
    public TransactionCurrency? Currency { get; init; }
    public TransactionInstitution? Institution { get; init; }
    public TransactionAccount? Account { get; init; }
    public bool IncludeInAnalysis { get; init; }
    public bool IsInternalTransfer { get; init; }
    public bool Marked { get; init; }
}

public sealed record TransactionCurrency
{
    public string? Code { get; init; }
    public string? Symbol { get; init; }
}

public sealed record TransactionInstitution
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
}

public sealed record TransactionAccount
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
}
