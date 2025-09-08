using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MemCacheLoadTester.Configuration;

/// <summary>
/// Thread-safe local tracker for keys that have been successfully SET by our clients.
/// This maintains a list of keys we know exist so GET and DELETE operations can target them.
/// No cache pre-warming needed - we build this list dynamically as SET operations succeed.
/// </summary>
public class SharedKeyTracker
{
    private readonly ConcurrentSet<string> _existingKeys;
    private readonly Random _random;
    private readonly object _lock = new();

    public SharedKeyTracker()
    {
        _existingKeys = new ConcurrentSet<string>();
        _random = new Random();
    }

    /// <summary>
    /// Registers a key as successfully stored in the cache.
    /// </summary>
    public void RegisterKey(string key)
    {
        _existingKeys.TryAdd(key);
    }

    /// <summary>
    /// Removes a key when it's successfully deleted from the cache.
    /// </summary>
    public void RemoveKey(string key)
    {
        _existingKeys.TryRemove(key);
    }

    /// <summary>
    /// Gets a random existing key, or null if no keys exist.
    /// </summary>
    public string? GetRandomExistingKey()
    {
        var keys = _existingKeys.ToArray();
        if (keys.Length == 0)
            return null;

        return keys[_random.Next(keys.Length)];
    }

    /// <summary>
    /// Gets the number of keys currently tracked as existing.
    /// </summary>
    public int KeyCount => _existingKeys.Count;

    /// <summary>
    /// Checks if a specific key is tracked as existing.
    /// </summary>
    public bool KeyExists(string key) => _existingKeys.Contains(key);
}

/// <summary>
/// Thread-safe set implementation for tracking keys.
/// </summary>
public class ConcurrentSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary = new();

    public bool TryAdd(T item) => _dictionary.TryAdd(item, 0);
    public bool TryRemove(T item) => _dictionary.TryRemove(item, out _);
    public bool Contains(T item) => _dictionary.ContainsKey(item);
    public int Count => _dictionary.Count;
    public T[] ToArray() => _dictionary.Keys.ToArray();
}

/// <summary>
/// Generates keys according to the specified pattern.
/// </summary>
public class KeyGenerator
{
    private readonly KeyConfig _config;
    private readonly Random _random;
    private readonly SharedKeyTracker _keyTracker;
    private long _sequentialCounter;

    public KeyGenerator(KeyConfig config, SharedKeyTracker keyTracker)
    {
        _config = config;
        _random = new Random();
        _keyTracker = keyTracker;
        _sequentialCounter = config.StartingNumber;
    }

    /// <summary>
    /// Generates a key according to the configured pattern.
    /// </summary>
    public string GenerateKey()
    {
        return _config.Pattern switch
        {
            KeyPattern.Sequential => GenerateSequentialKey(),
            KeyPattern.Random => GenerateRandomKey(),
            KeyPattern.UUID => GenerateUuidKey(),
            _ => GenerateSequentialKey()
        };
    }

    /// <summary>
    /// Gets a key from our local list of keys that we know exist.
    /// Returns null if no keys have been successfully SET yet.
    /// </summary>
    public string? GenerateExistingKey()
    {
        // Get a random key from our local list of successfully SET keys
        return _keyTracker.GetRandomExistingKey();
    }

    /// <summary>
    /// Registers a key as successfully stored in the cache.
    /// </summary>
    public void RegisterStoredKey(string key)
    {
        _keyTracker.RegisterKey(key);
    }

    /// <summary>
    /// Removes a key when it's successfully deleted from the cache.
    /// </summary>
    public void RemoveDeletedKey(string key)
    {
        _keyTracker.RemoveKey(key);
    }

    /// <summary>
    /// Gets the number of keys currently tracked as existing.
    /// </summary>
    public int ExistingKeyCount => _keyTracker.KeyCount;

    private string GenerateSequentialKey()
    {
        var key = $"{_config.Prefix}{_sequentialCounter}";
        _sequentialCounter++;
        
        // Wrap around if we exceed key space size
        if (_sequentialCounter > _config.KeySpaceSize)
        {
            _sequentialCounter = _config.StartingNumber;
        }
        
        return TruncateKey(key);
    }

    private string GenerateRandomKey()
    {
        var keyNumber = _random.NextInt64(1, _config.KeySpaceSize + 1);
        return TruncateKey($"{_config.Prefix}{keyNumber}");
    }

    private string GenerateUuidKey()
    {
        var uuid = Guid.NewGuid().ToString("N");
        return TruncateKey($"{_config.Prefix}{uuid}");
    }

    private string TruncateKey(string key)
    {
        if (key.Length > _config.MaxLength)
        {
            return key[.._config.MaxLength];
        }
        return key;
    }
}

/// <summary>
/// Generates values according to the specified pattern.
/// </summary>
public class ValueGenerator
{
    private readonly ValueConfig _config;
    private readonly Random _random;
    private readonly byte[] _randomBuffer;

    public ValueGenerator(ValueConfig config)
    {
        _config = config;
        _random = new Random();
        _randomBuffer = new byte[Math.Max(config.MaxSizeBytes, 1024)];
    }

    /// <summary>
    /// Generates a value according to the configured pattern.
    /// </summary>
    public byte[] GenerateValue()
    {
        var size = _config.Pattern == ValuePattern.Fixed 
            ? _config.FixedSizeBytes 
            : _random.Next(_config.MinSizeBytes, _config.MaxSizeBytes + 1);

        return _config.Pattern switch
        {
            ValuePattern.Random => GenerateRandomValue(size),
            ValuePattern.Fixed => GenerateRandomValue(size),
            ValuePattern.Compressible => GenerateCompressibleValue(size),
            _ => GenerateRandomValue(size)
        };
    }

    private byte[] GenerateRandomValue(int size)
    {
        var value = new byte[size];
        _random.NextBytes(value);
        return value;
    }

    private byte[] GenerateCompressibleValue(int size)
    {
        // Generate compressible data by repeating patterns
        var value = new byte[size];
        var pattern = Encoding.UTF8.GetBytes("LoadTest123");
        
        for (int i = 0; i < size; i++)
        {
            value[i] = pattern[i % pattern.Length];
        }
        
        return value;
    }
}

/// <summary>
/// Manages operation selection based on workload configuration.
/// </summary>
public class WorkloadSelector
{
    private readonly WorkloadConfig _config;
    private readonly Random _random;

    public WorkloadSelector(WorkloadConfig config)
    {
        _config = config;
        _config.Validate();
        _random = new Random();
    }

    /// <summary>
    /// Selects the next operation type based on configured percentages.
    /// </summary>
    public OperationType SelectOperation()
    {
        var roll = _random.Next(1, 101); // 1-100
        
        if (roll <= _config.SetPercentage)
        {
            return OperationType.Set;
        }
        else if (roll <= _config.SetPercentage + _config.GetPercentage)
        {
            return OperationType.Get;
        }
        else
        {
            return OperationType.Delete;
        }
    }

    /// <summary>
    /// Determines if a GET operation should result in a cache hit.
    /// </summary>
    public bool ShouldCacheHit()
    {
        return _random.Next(1, 101) <= _config.CacheHitRatio;
    }
}

/// <summary>
/// Types of operations that can be performed.
/// </summary>
public enum OperationType
{
    Set,
    Get,
    Delete
}