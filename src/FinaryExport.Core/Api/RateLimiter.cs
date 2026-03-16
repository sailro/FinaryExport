namespace FinaryExport.Api;

// Token bucket rate limiter. ~5 requests/second max.
// API analysis: browser makes ~2.5 req/s with no rate limit headers observed.
public sealed class RateLimiter
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private DateTime _lastRequest = DateTime.MinValue;
	private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(200); // 5 req/s

	public async Task WaitAsync(CancellationToken ct)
	{
		await _semaphore.WaitAsync(ct);
		try
		{
			var elapsed = DateTime.UtcNow - _lastRequest;
			if (elapsed < _interval)
				await Task.Delay(_interval - elapsed, ct);
			_lastRequest = DateTime.UtcNow;
		}
		finally
		{
			_semaphore.Release();
		}
	}
}
