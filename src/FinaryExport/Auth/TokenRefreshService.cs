using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Auth;

// Background service that refreshes the Clerk JWT every 50 seconds.
// Uses PeriodicTimer for cooperative cancellation and clean shutdown.
public sealed class TokenRefreshService(ClerkAuthClient authClient, ILogger<TokenRefreshService> logger)
	: IHostedService, IDisposable
{
	private readonly TimeSpan _interval = TimeSpan.FromSeconds(50);
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_interval);
        _executingTask = RefreshLoopAsync(_cts.Token);
        logger.LogDebug("Token refresh service started (interval: {Interval}s)", _interval.TotalSeconds);
        return Task.CompletedTask;
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        while (await _timer!.WaitForNextTickAsync(ct))
        {
            // Don't attempt refresh until login has completed
            if (string.IsNullOrEmpty(authClient.SessionId))
                continue;

            try
            {
                await authClient.RefreshTokenAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Token refresh failed, will retry next interval");
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Token refresh service stopping...");
        _cts?.Cancel();
        _timer?.Dispose();

        if (_executingTask is not null)
        {
            try
            {
                await _executingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _timer?.Dispose();
    }
}
