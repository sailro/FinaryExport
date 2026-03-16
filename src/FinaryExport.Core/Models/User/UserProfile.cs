namespace FinaryExport.Models.User;

public sealed record UserProfile
{
	public string? Slug { get; init; }
	public string? Firstname { get; init; }
	public string? Lastname { get; init; }
	public string? Fullname { get; init; }
	public string? Email { get; init; }
	public string? Country { get; init; }
	public string? Birthdate { get; init; }
	public int? Age { get; init; }
	public bool IsOtpEnabled { get; init; }
	public string? AccessLevel { get; init; }
	public bool PlusAccess { get; init; }
	public bool ProAccess { get; init; }
	public string? SubscriptionStatus { get; init; }
	public UiConfiguration? UiConfiguration { get; init; }
}

public sealed record UiConfiguration
{
	public DisplayCurrencyInfo? DisplayCurrency { get; init; }
}

public sealed record DisplayCurrencyInfo
{
	public string? Code { get; init; }
	public string? Symbol { get; init; }
}
