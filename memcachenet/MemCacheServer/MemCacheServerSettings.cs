namespace memcachenet.MemCacheServer;

/// <summary>
/// Configuration settings for the MemCache server instance.
/// </summary>
public class MemCacheServerSettings
{
    /// <summary>
    /// Gets or sets the port number on which the MemCache server listens for client connections.
    /// </summary>
    /// <value>The default port is 11211, which is the standard memcached port.</value>
    public int Port { get; set; } = 11211;

    /// <summary>
    /// Gets or sets the maximum number of keys that can be stored in the cache.
    /// </summary>
    /// <value>The default maximum is 300 keys.</value>
    public int MaxKeys { get; set; } = 300;

    /// <summary>
    /// Gets or sets the default expiration time for cached items in seconds.
    /// </summary>
    /// <value>The default expiration time is 60 seconds.</value>
    public int ExpirationTimeSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of concurrent client connections allowed.
    /// </summary>
    /// <value>The default maximum is 10 concurrent connections.</value>
    public int MaxConcurrentConnections { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum size in bytes for data values stored in the cache.
    /// </summary>
    /// <value>The default maximum is 102400 bytes (100 KB).</value>
    public int MaxDataSizeBytes { get; set; } = 102400;

    /// <summary>
    /// Gets or sets the maximum size in bytes for cache keys.
    /// </summary>
    /// <value>The default maximum is 300 bytes.</value>
    public int MaxKeySizeBytes { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum size of the cache in bytes.
    /// </summary>
    /// <value>The default maximum is 1073741824 bytes (1GB).</value>
    public int MaxTotalCacheSizeBytes { get; set; } = 1073741824; // 1 GB
}