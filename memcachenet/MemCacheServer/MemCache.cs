using System.Collections.Concurrent;
using System.Reflection.Metadata;
using memcachenet.MemCacheServer.EvictionPolicyManager;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Abstraction of the in-memory cache used by the memcachenet server.
/// Provides asynchronous operations to set, get, and delete values by key.
/// </summary>
public interface IMemCache
{
    /// <summary>
    /// Stores or replaces a value for the specified key.
    /// </summary>
    /// <param name="key">The key to associate with the value. Must be non-null and within protocol limits.</param>
    /// <param name="value">The raw bytes to store.</param>
    /// <param name="Flags">An opaque 32-bit value provided by clients and returned on retrieval.</param>
    Task<bool> SetAsync(string key, byte[] value, uint Flags);

    /// <summary>
    /// Attempts to retrieve the value for the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>
    /// The stored bytes if the key exists; otherwise an empty byte array. The method never throws for missing keys.
    /// </returns>
    Task<MemCacheItem?> TryGetAsync(string key);


    /// <summary>
    /// Deletes the value associated with the specified key, if it exists.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// Deletes a N given amount of keys that are expired.
    /// </summary>
    /// <param name="sampleSize">The number of keys to delete</param>
    Task DeleteExpiredKeysAsync(int sampleSize = 20);
}

/// <summary>
/// Fluent builder for configuring and creating <see cref="MemCache"/> instances.
/// </summary>
public class MemCacheBuilder
{
    private const int DefaultMaxKeys = 300;

    private const int DefaultMaxCacheSize = 102400;
    
    private readonly TimeSpan _defaultExpirationTime = TimeSpan.FromHours(1);
    private readonly IEvictionPolicyManager _defaultEvictionPolicyManager = new LruEvictionPolicyManager();
    
    private int _maxKeys;

    private int _maxCacheSize;
    private TimeSpan _expirationTime;
    private IEvictionPolicyManager _evictionPolicyManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemCacheBuilder"/> class with sensible defaults.
    /// </summary>
    public MemCacheBuilder()
    {
        _maxKeys = DefaultMaxKeys;
        _maxCacheSize = DefaultMaxCacheSize;
        _expirationTime = _defaultExpirationTime;
        _evictionPolicyManager = _defaultEvictionPolicyManager;
    }

    /// <summary>
    /// Sets the maximum number of keys the cache may hold before evicting based on the configured policy.
    /// </summary>
    /// <param name="maxKeys">Maximum number of distinct keys allowed.</param>
    /// <returns>The same builder instance for chaining.</returns>
    public MemCacheBuilder WithMaxKeys(int maxKeys)
    {
        _maxKeys = maxKeys;
        return this;
    }

    /// <summary>
    /// Sets the maximum total size of the values in the cache before not allowing new keys.
    /// </summary>
    /// <param name="maxCacheSize">Maximum total size for the values of the cache.</param>
    /// <returns>The same builder instance for chaining.</returns>
    public MemCacheBuilder WithMaxCacheSize(int maxCacheSize)
    {
        _maxCacheSize = maxCacheSize;
        return this;
    }
    
    /// <summary>
    /// Sets the default expiration time applied to new entries.
    /// </summary>
    /// <param name="expirationTime">Duration after which entries are considered expired.</param>
    /// <returns>The same builder instance for chaining.</returns>
    public MemCacheBuilder WithExpirationTime(TimeSpan expirationTime)
    {
        _expirationTime = expirationTime;
        return this;
    }

    /// <summary>
    /// Sets the expiration/eviction policy manager responsible for deciding which key to remove when capacity is reached.
    /// </summary>
    /// <param name="policyManager">The policy manager implementation (e.g., LRU).</param>
    /// <returns>The same builder instance for chaining.</returns>
    public MemCacheBuilder WithExpirationPolicy(IEvictionPolicyManager policyManager)
    {
        this._evictionPolicyManager = policyManager;
        return this;
    }
    
    /// <summary>
    /// Builds a <see cref="MemCache"/> instance using the configured options.
    /// </summary>
    public MemCache Build()
    {
        return new MemCache(_maxKeys, _maxCacheSize, _expirationTime, _evictionPolicyManager);
    }
}

/// <summary>
/// Thread-safe in-memory key-value cache with a pluggable expiration/eviction policy.
/// </summary>
public class MemCache : IMemCache
{
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    private readonly int _maxKeys;
    private readonly int _maxCacheSize;
    private readonly TimeSpan _expirationTime;
    private readonly ConcurrentDictionary<string, MemCacheItem> _cache;
    private readonly IEvictionPolicyManager _evictionPolicyManager;

    private int _cacheSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemCache"/> class.
    /// </summary>
    /// <param name="maxKeys">Maximum number of keys allowed before eviction.</param>
    /// <param name="maxCacheSize">Maximum total size of the values in the cache.</param>
    /// <param name="expirationTime">Default TTL applied to entries when set.</param>
    /// <param name="evictionPolicyManager">Policy manager used to track/access eviction order.</param>
    public MemCache(int maxKeys, int maxCacheSize, TimeSpan expirationTime, IEvictionPolicyManager evictionPolicyManager)
    {
        _maxKeys = maxKeys;
        _maxCacheSize = maxCacheSize;
        _expirationTime = expirationTime;
        _evictionPolicyManager = evictionPolicyManager;
        _cache = new ConcurrentDictionary<string, MemCacheItem>();
        _cacheSize = 0;
    }

    /// <summary>
    /// Stores or replaces a value for the specified key. If capacity is reached, evicts one key according to the policy.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value bytes to store.</param>
    /// <param name="Flags">Opaque client flags to persist with the value.</param>
    public async Task<bool> SetAsync(string key, byte[] value, uint Flags)
    {
        if (_cacheSize + value.Length > _maxCacheSize)
        {
            return false;
        }
        // lock so it can safely do the operation
        await _cacheLock.WaitAsync();

        // Check if we reached the max number of keys
        if (_cache.Count == _maxKeys)
        {
            // Find the key to be removed based on the expiration policy
            // Remove the key
            var keyToRemove = _evictionPolicyManager.KeyToRemove();
            _evictionPolicyManager.Delete(keyToRemove);
            _cache.Remove(keyToRemove, out _);
        }

        try
        {
            _cache[key] = new MemCacheItem
            {
                Value = value,
                Flags = Flags,
                Expiration = DateTime.Now.Add(_expirationTime)
            };
            _evictionPolicyManager.Add(key);

            // update the total size of the cache
            _cacheSize += value.Length;
        }
        finally
        {
            // release the lock
            _cacheLock.Release();
        }

        return true;
    }

    /// <summary>
    /// Retrieves the value for the specified key, updating policy usage tracking.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The stored value bytes, or an empty array if the key is not found.</returns>
    public async Task<MemCacheItem?> TryGetAsync(string key)
    {
        if (_cache.TryGetValue(key, out var item))
        {
            // Check the expiration date
            if (item.Expiration > DateTime.Now)
            {
                _evictionPolicyManager.Delete(key);
                _cache.Remove(key, out _);
                return null;
            }
            await _cacheLock.WaitAsync();
            try
            {
                _evictionPolicyManager.Get(key);
            }
            finally
            {
                _cacheLock.Release();
            }
            
            return item;
        }
        
        return null;
    }

    /// <summary>
    /// Deletes the specified key from the cache and updates the policy state if present.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    public async Task<bool> DeleteAsync(string key)
    {
        if (_cache.Remove(key, out var item))
        {
            await _cacheLock.WaitAsync();
            try
            {
                _evictionPolicyManager.Delete(key);

                // update the total size of the cache
                _cacheSize -= item.Value.Length;
            }
            finally
            {
                _cacheLock.Release();
            }
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Deletes a N given amount of keys that are expired.
    /// </summary>
    /// <param name="sampleSize">The number of keys to delete</param>
    public async Task DeleteExpiredKeysAsync(int sampleSize = 20)
    {
        // Take a random sample of keys to check
        var keysToCheck = _cache.Keys.OrderBy(k => Guid.NewGuid()).Take(sampleSize);

        foreach (var key in keysToCheck)
        {
            // Check if the key was not already deleted and if it is expired
            if (_cache.TryGetValue(key, out var item) && item.Expiration < DateTime.Now)
            {
                // Key is expired, delete it
                await DeleteAsync(key);
            }
        }
    }
}

/// <summary>
/// Internal representation of a cached item.
/// </summary>
public class MemCacheItem
{
    /// <summary>
    /// Raw value bytes stored for the key.
    /// </summary>
    public byte[] Value = [];

    /// <summary>
    /// Opaque client-provided flags returned on retrieval.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// Absolute expiration timestamp for this entry.
    /// </summary>
    public DateTime Expiration;
}
