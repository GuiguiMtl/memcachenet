using System.Text.Json.Serialization;

namespace MemCacheLoadTester.Configuration;

/// <summary>
/// Configuration settings for load testing the MemCache server.
/// </summary>
public class LoadTestConfig
{
    /// <summary>
    /// Target server hostname or IP address.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Target server port.
    /// </summary>
    public int Port { get; set; } = 11211;

    /// <summary>
    /// Number of concurrent client connections to use.
    /// </summary>
    public int ConcurrentClients { get; set; } = 10;

    /// <summary>
    /// Duration of the load test in seconds. If 0, run indefinitely or until OperationCount is reached.
    /// </summary>
    public int DurationSeconds { get; set; } = 60;

    /// <summary>
    /// Total number of operations to perform. If 0, run for DurationSeconds.
    /// </summary>
    public long OperationCount { get; set; } = 0;

    /// <summary>
    /// Time to ramp up to full load in seconds.
    /// </summary>
    public int RampUpSeconds { get; set; } = 10;

    /// <summary>
    /// Workload distribution settings.
    /// </summary>
    public WorkloadConfig Workload { get; set; } = new();

    /// <summary>
    /// Key generation settings.
    /// </summary>
    public KeyConfig Keys { get; set; } = new();

    /// <summary>
    /// Value generation settings.
    /// </summary>
    public ValueConfig Values { get; set; } = new();

    /// <summary>
    /// Reporting and output settings.
    /// </summary>
    public ReportingConfig Reporting { get; set; } = new();
}

/// <summary>
/// Configuration for workload distribution (SET/GET/DELETE ratios).
/// </summary>
public class WorkloadConfig
{
    /// <summary>
    /// Percentage of SET operations (0-100).
    /// </summary>
    public int SetPercentage { get; set; } = 20;

    /// <summary>
    /// Percentage of GET operations (0-100).
    /// </summary>
    public int GetPercentage { get; set; } = 70;

    /// <summary>
    /// Percentage of DELETE operations (0-100).
    /// Note: SetPercentage + GetPercentage + DeletePercentage should equal 100.
    /// </summary>
    public int DeletePercentage { get; set; } = 10;

    /// <summary>
    /// For GET operations, the percentage of keys that should exist (cache hit ratio).
    /// </summary>
    public int CacheHitRatio { get; set; } = 80;

    /// <summary>
    /// Validates that percentages add up to 100.
    /// </summary>
    public void Validate()
    {
        if (SetPercentage + GetPercentage + DeletePercentage != 100)
        {
            throw new ArgumentException("Workload percentages must add up to 100");
        }
        if (SetPercentage < 0 || GetPercentage < 0 || DeletePercentage < 0)
        {
            throw new ArgumentException("Workload percentages must be non-negative");
        }
        if (CacheHitRatio < 0 || CacheHitRatio > 100)
        {
            throw new ArgumentException("Cache hit ratio must be between 0 and 100");
        }
    }
}

/// <summary>
/// Configuration for key generation patterns.
/// </summary>
public class KeyConfig
{
    /// <summary>
    /// Key prefix to use for generated keys.
    /// </summary>
    public string Prefix { get; set; } = "loadtest_";

    /// <summary>
    /// Key generation pattern: Sequential, Random, or UUID.
    /// </summary>
    public KeyPattern Pattern { get; set; } = KeyPattern.Sequential;

    /// <summary>
    /// For sequential keys, the starting number.
    /// </summary>
    public long StartingNumber { get; set; } = 1;

    /// <summary>
    /// Maximum key length in bytes.
    /// </summary>
    public int MaxLength { get; set; } = 250;

    /// <summary>
    /// Key space size - how many unique keys to cycle through.
    /// </summary>
    public long KeySpaceSize { get; set; } = 100000;
}

/// <summary>
/// Configuration for value generation patterns.
/// </summary>
public class ValueConfig
{
    /// <summary>
    /// Minimum value size in bytes.
    /// </summary>
    public int MinSizeBytes { get; set; } = 100;

    /// <summary>
    /// Maximum value size in bytes.
    /// </summary>
    public int MaxSizeBytes { get; set; } = 1000;

    /// <summary>
    /// Value generation pattern: Random, Fixed, or Compressible.
    /// </summary>
    public ValuePattern Pattern { get; set; } = ValuePattern.Random;

    /// <summary>
    /// For fixed pattern, the exact size to use.
    /// </summary>
    public int FixedSizeBytes { get; set; } = 512;
}

/// <summary>
/// Configuration for reporting and output.
/// </summary>
public class ReportingConfig
{
    /// <summary>
    /// How often to print progress updates in seconds.
    /// </summary>
    public int ProgressIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Export results to CSV file.
    /// </summary>
    public bool ExportCsv { get; set; } = false;

    /// <summary>
    /// Export results to JSON file.
    /// </summary>
    public bool ExportJson { get; set; } = true;

    /// <summary>
    /// Output directory for exported files.
    /// </summary>
    public string OutputDirectory { get; set; } = "./results";

    /// <summary>
    /// Include individual operation results in exports (can be large).
    /// </summary>
    public bool IncludeIndividualResults { get; set; } = false;
}

/// <summary>
/// Patterns for key generation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KeyPattern
{
    Sequential,
    Random,
    UUID
}

/// <summary>
/// Patterns for value generation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValuePattern
{
    Random,
    Fixed,
    Compressible
}