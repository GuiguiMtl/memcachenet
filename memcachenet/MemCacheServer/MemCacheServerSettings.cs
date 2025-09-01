namespace memcachenet.MemCacheServer;

public class MemCacheServerSettings
{
    public int Port { get; set; } = 11211;
    public int MaxKeys { get; set; } = 300;
    public int ExpirationTimeSeconds { get; set; } = 60;
    public int MaxConcurrentConnections { get; set; } = 10;
    public int MaxDataSizeBytes { get; set; } = 102400;
    public int MaxKeySizeBytes { get; set; } = 300;        
}