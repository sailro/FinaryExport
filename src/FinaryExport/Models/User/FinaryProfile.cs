namespace FinaryExport.Models.User;

// Represents one exportable profile (one membership within an organization).
public sealed record FinaryProfile(string OrgId, string MembershipId, string ProfileName);
