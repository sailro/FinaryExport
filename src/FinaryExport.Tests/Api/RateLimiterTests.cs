using System.Diagnostics;
using FinaryExport.Api;
using FluentAssertions;

namespace FinaryExport.Tests.Api;

public sealed class RateLimiterTests
{
	[Fact]
	public async Task WaitAsync_FirstCall_ReturnsImmediately()
	{
		var limiter = new RateLimiter();
		var sw = Stopwatch.StartNew();

		await limiter.WaitAsync(CancellationToken.None);

		sw.ElapsedMilliseconds.Should().BeLessThan(100, "first call should not delay");
	}

	[Fact]
	public async Task WaitAsync_SecondCallWithinInterval_Delays()
	{
		var limiter = new RateLimiter();

		await limiter.WaitAsync(CancellationToken.None);
		var sw = Stopwatch.StartNew();
		await limiter.WaitAsync(CancellationToken.None);

		// Should delay close to 200ms (the interval). Allow some tolerance.
		sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(100, "second call should delay to enforce rate limit");
	}

	[Fact]
	public async Task WaitAsync_RespectsCancellation()
	{
		var limiter = new RateLimiter();
		await limiter.WaitAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var act = async () => await limiter.WaitAsync(cts.Token);
		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task WaitAsync_ConcurrentCalls_Serialized()
	{
		var limiter = new RateLimiter();
		var completionOrder = new List<int>();
		var lockObj = new object();

		var tasks = Enumerable.Range(0, 5).Select(async i =>
		{
			await limiter.WaitAsync(CancellationToken.None);
			lock (lockObj)
			{
				completionOrder.Add(i);
			}
		}).ToArray();

		await Task.WhenAll(tasks);

		// All 5 tasks should complete (serialized by semaphore)
		completionOrder.Should().HaveCount(5);
	}

	[Fact]
	public async Task WaitAsync_AfterIntervalElapsed_ReturnsImmediately()
	{
		var limiter = new RateLimiter();

		await limiter.WaitAsync(CancellationToken.None);
		await Task.Delay(250); // Wait longer than 200ms interval
		var sw = Stopwatch.StartNew();
		await limiter.WaitAsync(CancellationToken.None);

		sw.ElapsedMilliseconds.Should().BeLessThan(100, "enough time has passed since last request");
	}

	[Fact]
	public async Task WaitAsync_EnforcesRoughly5ReqPerSecond()
	{
		var limiter = new RateLimiter();
		var sw = Stopwatch.StartNew();

		// Make 6 requests (first is immediate, 5 more need 200ms each = ~1000ms minimum)
		for (var i = 0; i < 6; i++)
			await limiter.WaitAsync(CancellationToken.None);

		sw.Stop();
		sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(800,
			"5 intervals of ~200ms should enforce rate limiting");
	}
}
