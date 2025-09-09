namespace memcachenet.MemCacheServer.MemCacheResponses;

/// <summary>
/// Base class for all MemCache command responses.
/// </summary>
public abstract class MemCacheResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the command was executed successfully.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the error message if the command failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
/// <summary>
/// Response for GET commands containing retrieved cache values.
/// </summary>
public class GetMemCacheCommandResponse : MemCacheResponse
{
    /// <summary>
    /// Gets or sets the list of cache values retrieved by the GET command.
    /// </summary>
    public required List<MemCacheValue> Values { get; set; }
}

/// <summary>
/// Response for SET commands indicating whether the key-value pair was successfully stored.
/// </summary>
public class SetMemCacheCommandResponse : MemCacheResponse {}

/// <summary>
/// Response for DELETE commands indicating whether the key was successfully removed.
/// </summary>
public class DeleteMemeCacheCommandReponse : MemCacheResponse { }

/// <summary>
/// Response for invalid or unrecognized commands.
/// </summary>
public class InvalidMemCacheCommandReponse : MemCacheResponse { }

/// <summary>
/// Represents a cached value with its associated metadata.
/// </summary>
public class MemCacheValue
{
    /// <summary>
    /// Gets or sets the cache key.
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// Gets or sets the flags associated with the cached value.
    /// </summary>
    public required uint Flags { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the cached data in bytes.
    /// </summary>
    public required int Bytes { get; set; }
    
    /// <summary>
    /// Gets or sets the actual cached data as a byte array.
    /// </summary>
    public required byte[] Data { get; set; }
}