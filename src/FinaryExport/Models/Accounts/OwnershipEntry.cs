namespace FinaryExport.Models.Accounts;

public sealed record OwnershipEntry
{
	public decimal? Share { get; init; }
	public bool IsIndirect { get; init; }
	public OwnershipMembership? Membership { get; init; }
}

public sealed record OwnershipMembership
{
	public string? Id { get; init; }
	public string? MemberType { get; init; }
}
