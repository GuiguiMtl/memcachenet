using Microsoft.Extensions.Hosting;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Hosted background service responsible for periodically scanning the cache and
/// deleting expired keys.
/// </summary>
/// <remarks>
/// This service uses a <see cref="PeriodicTimer"/> to trigger scans at a fixed interval.
/// On each tick, it requests the underlying <see cref="IMemCache"/> to delete up to a specified
/// number of expired entries. The service starts when the host starts and stops when the host
/// is shutting down.
/// </remarks>
public class ExpirationManagerService : IHostedService
{
    private readonly IMemCache _memCache;
    private readonly int _numberOfKeysToDelete;
    private readonly PeriodicTimer _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpirationManagerService"/> class.
    /// </summary>
    /// <param name="memCache">The cache instance used to delete expired keys.</param>
    /// <param name="delayBetweenExecution">The delay between consecutive scans, expressed in seconds.
    /// </param>
    /// <param name="numberOfKeysToDelete">The maximum number of expired keys to delete per scan.</param>
    public ExpirationManagerService(IMemCache memCache, int delayBetweenExecution, int numberOfKeysToDelete)
    {
        _memCache = memCache;
        _numberOfKeysToDelete = numberOfKeysToDelete;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(delayBetweenExecution));
    }

    /// <summary>
    /// Starts the background scanning loop.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the start operation should be canceled.</param>
    /// <returns>A task that represents the execution of the background loop.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (await _timer.WaitForNextTickAsync(cancellationToken) && !cancellationToken.IsCancellationRequested)
        {
            await ScanForExpiredKeysAsync();
        }
    }

    /// <summary>
    /// Scans the cache and deletes up to <see cref="_numberOfKeysToDelete"/> expired keys.
    /// </summary>
    private async Task ScanForExpiredKeysAsync()
    {
        await _memCache.DeleteExpiredKeysAsync(_numberOfKeysToDelete);
    }

    /// <summary>
    /// Stops the background service and releases its timer resources.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the stop operation should be canceled.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
        return Task.CompletedTask;
    }
}