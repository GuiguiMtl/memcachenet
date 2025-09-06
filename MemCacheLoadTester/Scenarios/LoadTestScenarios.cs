using MemCacheLoadTester.Configuration;

namespace MemCacheLoadTester.Scenarios;

/// <summary>
/// Predefined load testing scenarios for common use cases.
/// </summary>
public static class LoadTestScenarios
{
    /// <summary>
    /// Write-heavy scenario: 80% SET, 15% GET, 5% DELETE
    /// Simulates applications with high write load like logging or analytics.
    /// </summary>
    public static LoadTestConfig WriteHeavy(int concurrentClients = 20, int durationSeconds = 120)
    {
        return new LoadTestConfig
        {
            ConcurrentClients = concurrentClients,
            DurationSeconds = durationSeconds,
            RampUpSeconds = 15,
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 80,
                GetPercentage = 15,
                DeletePercentage = 5,
                CacheHitRatio = 70
            },
            
            Keys = new KeyConfig
            {
                Prefix = "write_test_",
                Pattern = KeyPattern.Sequential,
                KeySpaceSize = 1000000,
                MaxLength = 100
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 500,
                MaxSizeBytes = 2000,
                Pattern = ValuePattern.Random
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 10,
                ExportJson = true,
                ExportCsv = false,
                OutputDirectory = "./results/write-heavy"
            }
        };
    }

    /// <summary>
    /// Read-heavy scenario: 10% SET, 85% GET, 5% DELETE
    /// Simulates applications with high read load like web caching.
    /// </summary>
    public static LoadTestConfig ReadHeavy(int concurrentClients = 50, int durationSeconds = 120)
    {
        return new LoadTestConfig
        {
            ConcurrentClients = concurrentClients,
            DurationSeconds = durationSeconds,
            RampUpSeconds = 20,
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 10,
                GetPercentage = 85,
                DeletePercentage = 5,
                CacheHitRatio = 90  // High hit ratio for read-heavy
            },
            
            Keys = new KeyConfig
            {
                Prefix = "read_test_",
                Pattern = KeyPattern.Random,
                KeySpaceSize = 100000,
                MaxLength = 80
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 200,
                MaxSizeBytes = 1000,
                Pattern = ValuePattern.Random
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 5,
                ExportJson = true,
                ExportCsv = true,
                OutputDirectory = "./results/read-heavy"
            }
        };
    }

    /// <summary>
    /// Mixed workload scenario: 30% SET, 60% GET, 10% DELETE
    /// Balanced workload for general purpose testing.
    /// </summary>
    public static LoadTestConfig Mixed(int concurrentClients = 30, int durationSeconds = 180)
    {
        return new LoadTestConfig
        {
            ConcurrentClients = concurrentClients,
            DurationSeconds = durationSeconds,
            RampUpSeconds = 15,
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 30,
                GetPercentage = 60,
                DeletePercentage = 10,
                CacheHitRatio = 75
            },
            
            Keys = new KeyConfig
            {
                Prefix = "mixed_test_",
                Pattern = KeyPattern.Sequential,
                KeySpaceSize = 500000,
                MaxLength = 120
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 100,
                MaxSizeBytes = 1500,
                Pattern = ValuePattern.Random
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 10,
                ExportJson = true,
                ExportCsv = true,
                OutputDirectory = "./results/mixed",
                IncludeIndividualResults = false
            }
        };
    }

    /// <summary>
    /// Stress test scenario: High concurrency with mixed operations
    /// Designed to push the server to its limits.
    /// </summary>
    public static LoadTestConfig StressTest(int concurrentClients = 100, int durationSeconds = 300)
    {
        return new LoadTestConfig
        {
            ConcurrentClients = concurrentClients,
            DurationSeconds = durationSeconds,
            RampUpSeconds = 30,
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 25,
                GetPercentage = 65,
                DeletePercentage = 10,
                CacheHitRatio = 70
            },
            
            Keys = new KeyConfig
            {
                Prefix = "stress_",
                Pattern = KeyPattern.Random,
                KeySpaceSize = 2000000,
                MaxLength = 250
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 1000,
                MaxSizeBytes = 10000, // Larger values for stress
                Pattern = ValuePattern.Random
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 15,
                ExportJson = true,
                ExportCsv = true,
                OutputDirectory = "./results/stress",
                IncludeIndividualResults = false
            }
        };
    }

    /// <summary>
    /// Latency-focused scenario: Lower concurrency, smaller operations
    /// Optimized to measure minimum latency characteristics.
    /// </summary>
    public static LoadTestConfig LowLatency(int concurrentClients = 5, int durationSeconds = 120)
    {
        return new LoadTestConfig
        {
            ConcurrentClients = concurrentClients,
            DurationSeconds = durationSeconds,
            RampUpSeconds = 5,
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 20,
                GetPercentage = 75,
                DeletePercentage = 5,
                CacheHitRatio = 95
            },
            
            Keys = new KeyConfig
            {
                Prefix = "latency_",
                Pattern = KeyPattern.Sequential,
                KeySpaceSize = 10000,
                MaxLength = 50
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 50,
                MaxSizeBytes = 200, // Small values for low latency
                Pattern = ValuePattern.Fixed,
                FixedSizeBytes = 100
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 5,
                ExportJson = true,
                ExportCsv = true,
                OutputDirectory = "./results/low-latency",
                IncludeIndividualResults = true // Capture all ops for latency analysis
            }
        };
    }

    /// <summary>
    /// Connection limit test: Tests server's concurrent connection handling
    /// Uses many clients with low operation rate each.
    /// </summary>
    public static LoadTestConfig ConnectionLimit(int concurrentClients = 200, int durationSeconds = 60)
    {
        return new LoadTestConfig
        {
            ConcurrentClients = concurrentClients,
            DurationSeconds = durationSeconds,
            RampUpSeconds = 45, // Slow ramp up to test connection handling
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 50,
                GetPercentage = 45,
                DeletePercentage = 5,
                CacheHitRatio = 80
            },
            
            Keys = new KeyConfig
            {
                Prefix = "conn_test_",
                Pattern = KeyPattern.UUID,
                KeySpaceSize = 50000,
                MaxLength = 100
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 100,
                MaxSizeBytes = 500,
                Pattern = ValuePattern.Random
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 10,
                ExportJson = true,
                ExportCsv = false,
                OutputDirectory = "./results/connection-limit"
            }
        };
    }

    /// <summary>
    /// Gets a dictionary of all predefined scenarios for easy selection.
    /// </summary>
    public static Dictionary<string, Func<LoadTestConfig>> GetScenarios()
    {
        return new Dictionary<string, Func<LoadTestConfig>>
        {
            ["write-heavy"] = () => WriteHeavy(),
            ["read-heavy"] = () => ReadHeavy(),
            ["mixed"] = () => Mixed(),
            ["stress"] = () => StressTest(),
            ["low-latency"] = () => LowLatency(),
            ["connection-limit"] = () => ConnectionLimit()
        };
    }

    /// <summary>
    /// Gets a list of available scenario names.
    /// </summary>
    public static IEnumerable<string> GetScenarioNames()
    {
        return GetScenarios().Keys;
    }
}