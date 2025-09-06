using System.Diagnostics;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Provides OpenTelemetry activity sources and telemetry helpers for the MemCache server.
/// </summary>
public static class MemCacheTelemetry
{
    /// <summary>
    /// The name of the telemetry source for MemCache operations.
    /// </summary>
    public const string ActivitySourceName = "MemCacheNet";
    
    /// <summary>
    /// The version of the telemetry source.
    /// </summary>
    public const string Version = "1.0.0";
    
    /// <summary>
    /// Activity source for MemCache server operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);
    
    /// <summary>
    /// Activity names for different operations.
    /// </summary>
    public static class ActivityNames
    {
        public const string ServerStart = "memcache.server.start";
        public const string ServerStop = "memcache.server.stop";
        public const string ConnectionSession = "memcache.connection.session";
        public const string ConnectionAccept = "memcache.connection.accept";
        public const string ConnectionHandle = "memcache.connection.handle";
        public const string CommandRead = "memcache.command.read";
        public const string CommandParse = "memcache.command.parse";
        public const string CommandHandle = "memcache.command.handle";
        public const string CacheGet = "memcache.cache.get";
        public const string CacheSet = "memcache.cache.set";
        public const string CacheDelete = "memcache.cache.delete";
        public const string PipelineFill = "memcache.pipeline.fill";
        public const string PipelineRead = "memcache.pipeline.read";
    }
    
    /// <summary>
    /// Tag names for telemetry attributes.
    /// </summary>
    public static class Tags
    {
        public const string ServerPort = "memcache.server.port";
        public const string ConnectionId = "memcache.connection.id";
        public const string CommandType = "memcache.command.type";
        public const string CacheKey = "memcache.cache.key";
        public const string CacheHit = "memcache.cache.hit";
        public const string ErrorType = "memcache.error.type";
        public const string DataSize = "memcache.data.size";
        public const string ClientEndpoint = "memcache.client.endpoint";
    }
}