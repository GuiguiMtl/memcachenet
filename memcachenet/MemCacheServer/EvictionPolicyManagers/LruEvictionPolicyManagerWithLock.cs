namespace memcachenet.MemCacheServer.EvictionPolicyManagers;

public class LruEvictionPolicyManagerWithLock :  IEvictionPolicyManager
{
    private readonly LruEvictionPolicyManager _innerManager = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> KeyToRemove()
    {
        await _lock.WaitAsync();
        try
        {
            return await _innerManager.KeyToRemove();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task Add(string key)
    {
        await _lock.WaitAsync();
        try
        {
            await _innerManager.Add(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task Delete(string key)
    {
        await _lock.WaitAsync();
        try
        {
            await _innerManager.Delete(key);
        }
        finally
        {
            _lock.Release();
        }
    }
    public async Task Get(string key)
    {
        await _lock.WaitAsync();
        try
        {
            await _innerManager.Get(key);
        }
        finally
        {
            _lock.Release();
        }
    }
}