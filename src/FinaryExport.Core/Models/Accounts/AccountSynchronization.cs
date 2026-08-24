namespace FinaryExport.Models.Accounts;

// Connection synchronization metadata returned by the documented
// /users/me/synchronizations endpoint. It is shared Core data: consumers may
// use it to explain stale account values without changing account semantics.
public sealed record AccountSynchronization
{
	public string? CorrelationId { get; init; }
	public string? State { get; init; }
	public string? StateMessage { get; init; }
	public string? ConnectionState { get; init; }
	public DateTimeOffset? LastSyncAt { get; init; }
	public DateTimeOffset? LastSuccessfulSyncAt { get; init; }
	public string? SyncStatus { get; init; }
}
