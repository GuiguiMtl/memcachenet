namespace memcachenet.MemCacheServer.Settings;

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

    /// <summary>
    /// Gets or sets the idle connection timeout in seconds.
    /// Connections that are idle for longer than this period will be closed.
    /// </summary>
    /// <value>The default is 0 seconds, which disables idle timeout.</value>
    public int ConnectionIdleTimeoutSeconds { get; set; } = 0; // 0 = disabled

    /// <summary>
    /// Gets or sets the read timeout in seconds for incomplete commands.
    /// If a command is not completed within this time, the connection will be closed.
    /// </summary>
    /// <value>The default is 30 seconds.</value>
    public int ReadTimeoutSeconds { get; set; } = 30;
}