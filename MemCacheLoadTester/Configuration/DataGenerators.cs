using System.Security.Cryptography;
using System.Text;

namespace MemCacheLoadTester.Configuration;

/// <summary>
/// Generates keys according to the specified pattern.
/// </summary>
public class KeyGenerator
{
    private readonly KeyConfig _config;
    private readonly Random _random;
    private long _sequentialCounter;

    public KeyGenerator(KeyConfig config)
    {
        _config = config;
        _random = new Random();
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
    /// Generates a key that should exist (for cache hits).
    /// </summary>
    public string GenerateExistingKey()
    {
        // Generate a key from the existing key space
        var keyNumber = _random.NextInt64(1, Math.Min(_sequentialCounter, _config.KeySpaceSize));
        return $"{_config.Prefix}{keyNumber}";
    }

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