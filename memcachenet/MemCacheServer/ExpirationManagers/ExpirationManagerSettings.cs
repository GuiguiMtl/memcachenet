namespace memcachenet.MemCacheServer.ExpirationManagers;

public class ExpirationManagerSettings
{
    /// <summary>
    // The delay between consecutive scans, expressed in seconds.
    /// </summary>
    /// <value>The default delay is 60 seconds.</value>
    public int DelayBetweenExecutionSeconds { get; init; } = 60;

    /// <summary>
    // The maximum number of expired keys to delete per scan.
    /// </summary>
    /// <value>The default is 10 keys.</value>
    public int NumberOfKeysToDelete { get; init; } = 10;
}