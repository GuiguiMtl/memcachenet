namespace memcachenet.MemCacheServer;

public interface IMemCacheCommandHandler<in TMemCacheCommand> where TMemCacheCommand : IMemCacheCommand
{
    void HandleCommand(TMemCacheCommand command);
}

public class MemCacheCommandHandler : IMemCacheCommandHandler<GetMemCacheCommand>,
    IMemCacheCommandHandler<SetMemCacheCommand>,
    IMemCacheCommandHandler<DeleteMemCacheCommand>, 
    IMemCacheCommandHandler<InvalidMemCacheCommand> 
{
    public void HandleCommand(GetMemCacheCommand command)
    {
        return;
    }

    public void HandleCommand(SetMemCacheCommand command)
    {
        return;
    }

    public void HandleCommand(DeleteMemCacheCommand command)
    {
        return;
    }

    public void HandleCommand(InvalidMemCacheCommand command)
    {
        return;
    }
}