namespace memcachenet.MemCacheServer.EvictionPolicyManager;

// provide the Last Recently Used (LRU) implementation of the expiration policy 
public class LruEvictionPolicyManager : IEvictionPolicyManager
{
    private readonly Dictionary<string, LinkedListNode<string>> _lruMap = new();
    private readonly LinkedList<string> _lruIndex = new();

    public string KeyToRemove() => _lruIndex.Last?.Value ?? string.Empty;

    public void Add(string key)
    {
        var node = _lruIndex.AddFirst(key);
        _lruMap[key] = node;
    }

    public void Delete(string key)
    {
        if (_lruMap.TryGetValue(key, out var node))
        {
            _lruIndex.Remove(node);
            _lruMap.Remove(key);
        }
    }

    public void Get(string key)
    {
        if (_lruMap.TryGetValue(key, out var node))
        {
            _lruIndex.Remove(node);
            var newNode = _lruIndex.AddFirst(key);
            _lruMap[key] = newNode;
        }
    }
}