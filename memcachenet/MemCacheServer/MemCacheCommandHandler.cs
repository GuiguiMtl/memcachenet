namespace memcachenet.MemCacheServer;

public interface IMemCacheCommandHandler<in TMemCacheCommand, TMemCacheResponse> where TMemCacheCommand : IMemCacheCommand
{
    Task<TMemCacheResponse> HandleCommandAsync(TMemCacheCommand command);
}

public class MemCacheCommandHandler(IMemCache memCache) : IMemCacheCommandHandler<GetMemCacheCommand, GetMemCacheCommandResponse>,
    IMemCacheCommandHandler<SetMemCacheCommand, SetMemCacheCommandResponse>,
    IMemCacheCommandHandler<DeleteMemCacheCommand, DeleteMemeCacheCommandReponse>, 
    IMemCacheCommandHandler<InvalidMemCacheCommand, InvalidMemCacheCommandReponse> 
{
    private readonly IMemCache memCache = memCache;

    public async Task<GetMemCacheCommandResponse> HandleCommandAsync(GetMemCacheCommand command)
    {
        List<MemCacheValue> values = new();
        foreach (var key in command.Keys)
        {
            var item = await memCache.TryGetAsync(key);
            if (item != null)
            {
                values.Add(new MemCacheValue
                {
                    Key = key,
                    Flags = item.Flags,
                    Bytes = item.Value.Length,
                    Data = item.Value
                });
            }
        }
        return new GetMemCacheCommandResponse
        {
            Success = true,
            Values = values
        };
    }

    public async Task<SetMemCacheCommandResponse> HandleCommandAsync(SetMemCacheCommand command)
    {
        if (!await memCache.SetAsync(command.Key, command.Data, command.Flags))
        {
            return new SetMemCacheCommandResponse
            {
                ErrorMessage = "Max cache size reached",
                Success = false
            };
        }

        return new SetMemCacheCommandResponse
        {
            Success = true
        };
    }

    public async Task<DeleteMemeCacheCommandReponse> HandleCommandAsync(DeleteMemCacheCommand command)
    {
        var success = await memCache.DeleteAsync(command.Key);
        return new DeleteMemeCacheCommandReponse
        {
            Success = success
        };
    }

    public Task<InvalidMemCacheCommandReponse> HandleCommandAsync(InvalidMemCacheCommand command)
    {
        return Task.FromResult(new InvalidMemCacheCommandReponse
        {
            Success = false,
            ErrorMessage = "Invalid command"
        });
    }
}