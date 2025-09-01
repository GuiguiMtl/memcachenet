using System.Reflection.Metadata;
using System.Text;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Defines the contract for all MemCache commands that can be executed by the server.
/// </summary>
public interface IMemCacheCommand
{
    /// <summary>
    /// Handles the execution of the command asynchronously using the provided handler.
    /// </summary>
    /// <param name="handler">The command handler that will process this command.</param>
    /// <returns>A task containing the formatted response bytes to send back to the client.</returns>
    Task<byte []> HandleAsync(MemCacheCommandHandler handler);
}

/// <summary>
/// Base class for commands that support the 'noreply' flag to suppress response messages.
/// </summary>
public class NoReplyCacheCommand
{
    /// <summary>
    /// Gets or sets a value indicating whether the client requested no reply (noreply flag).
    /// </summary>
    public required bool NoReply;
}

/// <summary>
/// Represents a SET command to store a key-value pair in the cache.
/// </summary>
public class SetMemCacheCommand : NoReplyCacheCommand, IMemCacheCommand
{
    /// <summary>
    /// Gets or sets the cache key to store the data under.
    /// </summary>
    public required string Key;
    
    /// <summary>
    /// Gets or sets the data to be stored in the cache.
    /// </summary>
    public required byte[] Data;
    
    /// <summary>
    /// Gets or sets the flags associated with the stored data.
    /// </summary>
    public required uint Flags;
    
    /// <summary>
    /// Gets or sets the expiration time for the cached item.
    /// </summary>
    public required uint Expiration;

    /// <summary>
    /// The "STORED" response bytes for successful SET operations.
    /// </summary>
    private static byte[] STORED = Encoding.UTF8.GetBytes("STORED\r\n");

    /// <summary>
    /// Handles the SET command execution asynchronously.
    /// </summary>
    /// <param name="handler">The command handler to process the SET operation.</param>
    /// <returns>A task containing the formatted response bytes.</returns>
    public async Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {

        var response = await handler.HandleCommandAsync(this);
        return Format(response);
    }

    /// <summary>
    /// Formats the SET command response according to the memcached protocol.
    /// </summary>
    /// <param name="response">The response from the command handler.</param>
    /// <returns>The formatted response bytes.</returns>
    private byte[] Format(SetMemCacheCommandResponse response)
    {
        if (response.Success)
        {
            if (this.NoReply)
            {
                return [];
            }
            return STORED;
        }

        return Encoding.UTF8.GetBytes($"SERVER_ERROR {response.ErrorMessage}");
    }
}

/// <summary>
/// Represents a GET command to retrieve values from the cache for one or more keys.
/// </summary>
public class GetMemCacheCommand : IMemCacheCommand
{
    /// <summary>
    /// Gets or sets the array of cache keys to retrieve.
    /// </summary>
    public required string[] Keys;

    /// <summary>
    /// Handles the GET command execution asynchronously.
    /// </summary>
    /// <param name="handler">The command handler to process the GET operation.</param>
    /// <returns>A task containing the formatted response bytes with the retrieved values.</returns>
    public async Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {
        var response = await handler.HandleCommandAsync(this);
        return ResponseFormatter.FormatMemCacheCommandResponse(response);
    }
}

/// <summary>
/// Represents a DELETE command to remove a key-value pair from the cache.
/// </summary>
public class DeleteMemCacheCommand : NoReplyCacheCommand, IMemCacheCommand
{
    /// <summary>
    /// Gets or sets the cache key to delete.
    /// </summary>
    public required string Key;

    /// <summary>
    /// The "DELETED" response bytes for successful DELETE operations.
    /// </summary>
    private static byte[] DELETED = Encoding.UTF8.GetBytes("DELETED\r\n");

    /// <summary>
    /// Handles the DELETE command execution asynchronously.
    /// </summary>
    /// <param name="handler">The command handler to process the DELETE operation.</param>
    /// <returns>A task containing the formatted response bytes.</returns>
    public async Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {
        var response = await handler.HandleCommandAsync(this);
        if (response.Success)
        {
            if (this.NoReply)
            {
                return [];
            }
            return DELETED;
        }

        return Encoding.UTF8.GetBytes($"NOT_FOUND\r\n");
    }
}

/// <summary>
/// Represents an invalid or unrecognized command that could not be parsed.
/// </summary>
/// <param name="errorMessage">The error message describing why the command is invalid.</param>
public class InvalidMemCacheCommand(string errorMessage) : IMemCacheCommand
{
    /// <summary>
    /// Handles the invalid command by returning a CLIENT_ERROR response.
    /// </summary>
    /// <param name="handler">The command handler (not used for invalid commands).</param>
    /// <returns>A task containing the CLIENT_ERROR response bytes.</returns>
    public Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {
        return Task.FromResult(Encoding.UTF8.GetBytes($"CLIENT_ERROR {errorMessage}"));
    }
}