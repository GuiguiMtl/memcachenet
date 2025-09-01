namespace memcachenet.MemCacheServer.EvictionPolicyManager;

/// <summary>
/// Implements the Least Recently Used (LRU) eviction policy for cache management.
/// This policy removes the least recently accessed items first when the cache reaches its capacity limit.
/// </summary>
public class LruEvictionPolicyManager : IEvictionPolicyManager
{
    /// <summary>
    /// Maps cache keys to their corresponding nodes in the LRU linked list for O(1) access.
    /// </summary>
    private readonly Dictionary<string, LinkedListNode<string>> _lruMap = new();
    
    /// <summary>
    /// Maintains the LRU order with most recently used items at the front and least recently used at the back.
    /// </summary>
    private readonly LinkedList<string> _lruIndex = new();

    /// <summary>
    /// Gets the key of the least recently used item that should be removed from the cache.
    /// </summary>
    /// <returns>The key of the item to remove, or an empty string if no items exist.</returns>
    public string KeyToRemove() => _lruIndex.Last?.Value ?? string.Empty;

    /// <summary>
    /// Adds a new key to the LRU tracker, marking it as the most recently used item.
    /// </summary>
    /// <param name="key">The cache key to add to the LRU tracker.</param>
    public void Add(string key)
    {
        var node = _lruIndex.AddFirst(key);
        _lruMap[key] = node;
    }

    /// <summary>
    /// Removes a key from the LRU tracker when the corresponding cache item is deleted.
    /// </summary>
    /// <param name="key">The cache key to remove from the LRU tracker.</param>
    public void Delete(string key)
    {
        if (_lruMap.TryGetValue(key, out var node))
        {
            _lruIndex.Remove(node);
            _lruMap.Remove(key);
        }
    }

    /// <summary>
    /// Updates the LRU position of a key when it is accessed, moving it to the most recently used position.
    /// </summary>
    /// <param name="key">The cache key that was accessed.</param>
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