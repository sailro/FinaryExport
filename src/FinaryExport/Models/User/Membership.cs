namespace FinaryExport.Models.User;

public sealed record Membership
{
    public string? OrganizationId { get; init; }
    public string? MembershipId { get; init; }
}
