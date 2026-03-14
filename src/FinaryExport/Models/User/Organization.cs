namespace FinaryExport.Models.User;

public sealed record Organization
{
	public string? Id { get; init; }
	public string? Name { get; init; }
	public string? Slug { get; init; }
	public List<OrganizationMember>? Members { get; init; }
}

public sealed record OrganizationMember
{
	public string? Id { get; init; }
	public string? MemberType { get; init; }
	public OrganizationUser? User { get; init; }
}

public sealed record OrganizationUser
{
	public string? Fullname { get; init; }
	public bool IsOrganizationOwner { get; init; }
}
