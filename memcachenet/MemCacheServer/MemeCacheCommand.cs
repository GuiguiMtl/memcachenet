namespace memcachenet.MemCacheServer;

public interface IMemCacheCommand
{
    void Handle(MemCacheCommandHandler handler);
}

public class NoReplyCacheCommand
{
    public required bool NoReply;
}

public class SetMemCacheCommand : NoReplyCacheCommand, IMemCacheCommand
{
    public required string Key;
    public required byte[] Data;
    public required uint Flag;
    public required uint Expiration;
    
    public void Handle(MemCacheCommandHandler handler) => handler.HandleCommand(this);
}

public class GetMemCacheCommand : IMemCacheCommand
{
    public required string[] Keys;
    
    public void Handle(MemCacheCommandHandler handler) => handler.HandleCommand(this);
}

public class DeleteMemCacheCommand : NoReplyCacheCommand, IMemCacheCommand
{
    public required string Key;
    
    public void Handle(MemCacheCommandHandler handler) => handler.HandleCommand(this);
}

public class InvalidMemCacheCommand : IMemCacheCommand
{
    public void Handle(MemCacheCommandHandler handler) => handler.HandleCommand(this);
}