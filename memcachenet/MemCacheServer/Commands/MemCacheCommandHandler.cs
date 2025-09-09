using memcachenet.MemCacheServer.MemCacheResponses;
using Microsoft.Extensions.Logging;

namespace memcachenet.MemCacheServer.Commands;

/// <summary>
/// Defines a generic interface for handling MemCache commands.
/// </summary>
/// <typeparam name="TMemCacheCommand">The type of command to handle, must implement IMemCacheCommand.</typeparam>
/// <typeparam name="TMemCacheResponse">The type of response returned after handling the command.</typeparam>
public interface IMemCacheCommandHandler<in TMemCacheCommand, TMemCacheResponse> where TMemCacheCommand : IMemCacheCommand
{
    /// <summary>
    /// Handles the specified MemCache command asynchronously.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the command response.</returns>
    Task<TMemCacheResponse> HandleCommandAsync(TMemCacheCommand command);
}

/// <summary>
/// Provides implementations for handling various MemCache commands including GET, SET, DELETE, and invalid commands.
/// </summary>
/// <param name="memCache">The MemCache instance used to perform cache operations.</param>
public class MemCacheCommandHandler(IMemCache memCache, ILogger<MemCacheCommandHandler>? logger = null) : IMemCacheCommandHandler<GetMemCacheCommand, GetMemCacheCommandResponse>,
    IMemCacheCommandHandler<SetMemCacheCommand, SetMemCacheCommandResponse>,
    IMemCacheCommandHandler<DeleteMemCacheCommand, DeleteMemeCacheCommandReponse>, 
    IMemCacheCommandHandler<InvalidMemCacheCommand, InvalidMemCacheCommandReponse> 
{
    /// <summary>
    /// The MemCache instance used for cache operations.
    /// </summary>
    private readonly IMemCache memCache = memCache;
    
    /// <summary>
    /// Logger for debugging cache operations.
    /// </summary>
    private readonly ILogger<MemCacheCommandHandler>? _logger = logger;

    /// <summary>
    /// Handles a GET command to retrieve values from the cache for the specified keys.
    /// </summary>
    /// <param name="command">The GET command containing the keys to retrieve.</param>
    /// <returns>A response containing the retrieved cache values for existing keys.</returns>
    public async Task<GetMemCacheCommandResponse> HandleCommandAsync(GetMemCacheCommand command)
    {
        _logger?.LogDebug("Processing GET command for {KeyCount} keys: {Keys}", 
            command.Keys.Length, string.Join(", ", command.Keys));
        
        HashSet<string> processedKeys = [];
        List<MemCacheValue> values = [];
        var hits = 0;
        
        foreach (var key in command.Keys)
        {
            // Skip duplicate keys in the request
            if (!processedKeys.Add(key))
            {
                continue;
            }
            
            var item = await memCache.TryGetAsync(key);
            if (item != null)
            {
                hits++;
                
                _logger?.LogTrace("Cache HIT for key '{Key}', size: {Size} bytes", key, item.Value.Length);
                
                values.Add(new MemCacheValue
                {
                    Key = key,
                    Flags = item.Flags,
                    Bytes = item.Value.Length,
                    Data = item.Value
                });
            }
            else
            {
                _logger?.LogTrace("Cache MISS for key '{Key}'", key);
            }
        }
        
        _logger?.LogDebug("GET command completed: {Hits}/{Total} hits, hit ratio: {HitRatio:P2}", 
            hits, command.Keys.Length, command.Keys.Length > 0 ? (double)hits / command.Keys.Length : 0.0);
        
        return new GetMemCacheCommandResponse
        {
            Success = true,
            Values = values
        };
    }

    /// <summary>
    /// Handles a SET command to store a key-value pair in the cache.
    /// </summary>
    /// <param name="command">The SET command containing the key, data, and flags to store.</param>
    /// <returns>A response indicating whether the SET operation was successful or failed due to cache size limits.</returns>
    public async Task<SetMemCacheCommandResponse> HandleCommandAsync(SetMemCacheCommand command)
    {
        _logger?.LogDebug("Processing SET command for key '{Key}', size: {Size} bytes, flags: {Flags}", 
            command.Key, command.Data.Length, command.Flags);
        
        if (!await memCache.SetAsync(command.Key, command.Data, command.Flags))
        {
            _logger?.LogWarning("SET failed for key '{Key}': Max cache size reached", command.Key);
            
            return new SetMemCacheCommandResponse
            {
                ErrorMessage = "Max cache size reached",
                Success = false
            };
        }

        _logger?.LogTrace("SET successful for key '{Key}'", command.Key);
        
        return new SetMemCacheCommandResponse
        {
            Success = true
        };
    }

    /// <summary>
    /// Handles a DELETE command to remove a key-value pair from the cache.
    /// </summary>
    /// <param name="command">The DELETE command containing the key to remove.</param>
    /// <returns>A response indicating whether the DELETE operation was successful.</returns>
    public async Task<DeleteMemeCacheCommandReponse> HandleCommandAsync(DeleteMemCacheCommand command)
    {
        var success = await memCache.DeleteAsync(command.Key);
        
        _logger?.LogDebug("DELETE command for key '{Key}': {Success}", command.Key, success ? "SUCCESS" : "NOT_FOUND");
        
        return new DeleteMemeCacheCommandReponse
        {
            Success = success
        };
    }

    /// <summary>
    /// Handles an invalid command that could not be recognized or parsed.
    /// </summary>
    /// <param name="command">The invalid command that was received.</param>
    /// <returns>A response indicating the command was invalid with an error message.</returns>
    public Task<InvalidMemCacheCommandReponse> HandleCommandAsync(InvalidMemCacheCommand command)
    {
        _logger?.LogWarning("Invalid command received");
        
        return Task.FromResult(new InvalidMemCacheCommandReponse
        {
            Success = false,
            ErrorMessage = "Invalid command"
        });
    }
}