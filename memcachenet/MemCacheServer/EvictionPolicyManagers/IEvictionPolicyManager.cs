namespace memcachenet.MemCacheServer.EvictionPolicyManagers;

// Provide an interface to manage the expiration of the cache
public interface IEvictionPolicyManager
{
    Task<string> KeyToRemove();
    Task Add(string key);
    Task Delete(string key);
    Task Get(string key);
}