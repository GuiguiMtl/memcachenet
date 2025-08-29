namespace memcachenet.MemCacheServer.EvictionPolicyManager;

// Provide an interface to manage the expiration of the cache
public interface IEvictionPolicyManager
{
    string KeyToRemove();
    void Add(string key);
    void Delete(string key);
    void Get(string key);
}