using System.Reflection.Metadata;
using System.Text;

namespace memcachenet.MemCacheServer;

public interface IMemCacheCommand
{
    Task<byte []> HandleAsync(MemCacheCommandHandler handler);
}

public class NoReplyCacheCommand
{
    public required bool NoReply;
}

public class SetMemCacheCommand : NoReplyCacheCommand, IMemCacheCommand
{
    public required string Key;
    public required byte[] Data;
    public required uint Flags;
    public required uint Expiration;

    private static byte[] STORED = Encoding.UTF8.GetBytes("STORED\r\n");

    public async Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {

        var response = await handler.HandleCommandAsync(this);
        return Format(response);
    }

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

public class GetMemCacheCommand : IMemCacheCommand
{
    public required string[] Keys;

    public async Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {
        var response = await handler.HandleCommandAsync(this);
        return ResponseFormatter.FormatMemCacheCommandResponse(response);
    }
}

public class DeleteMemCacheCommand : NoReplyCacheCommand, IMemCacheCommand
{
    public required string Key;

    private static byte[] DELETED = Encoding.UTF8.GetBytes("DELETED\r\n");


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

public class InvalidMemCacheCommand(string errorMessage) : IMemCacheCommand
{
    public Task<byte[]> HandleAsync(MemCacheCommandHandler handler)
    {
        return Task.FromResult(Encoding.UTF8.GetBytes($"CLIENT_ERROR {errorMessage}"));
    }
}