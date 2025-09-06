using System.CommandLine;
using MemCacheLoadTester.Configuration;
using MemCacheLoadTester.Scenarios;

namespace MemCacheLoadTester;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        try
        {
            return args[0].ToLower() switch
            {
                "list" => ListScenarios(),
                "scenario" => await RunScenario(args),
                "custom" => await RunCustom(args),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => ShowHelp()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ShowHelp()
    {
        Console.WriteLine("MemCache Load Testing Tool");
        Console.WriteLine("A high-performance load testing tool for memcached-compatible servers");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  MemCacheLoadTester list");
        Console.WriteLine("  MemCacheLoadTester scenario <name> [options]");
        Console.WriteLine("  MemCacheLoadTester custom [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list                     List available scenarios");
        Console.WriteLine("  scenario <name>          Run a predefined scenario");
        Console.WriteLine("  custom                   Run a custom configuration");
        Console.WriteLine();
        Console.WriteLine("Scenario Options:");
        Console.WriteLine("  --host <host>           Target server hostname (default: localhost)");
        Console.WriteLine("  --port <port>           Target server port (default: 11211)");
        Console.WriteLine("  --clients <count>       Number of concurrent clients");
        Console.WriteLine("  --duration <seconds>    Test duration in seconds");
        Console.WriteLine("  --export <format>       Export format: json, csv, both");
        Console.WriteLine("  --log <path>            Enable file logging to specified path");
        Console.WriteLine();
        Console.WriteLine("Custom Options:");
        Console.WriteLine("  --host <host>           Target server hostname (default: localhost)");
        Console.WriteLine("  --port <port>           Target server port (default: 11211)");
        Console.WriteLine("  --clients <count>       Number of concurrent clients (default: 10)");
        Console.WriteLine("  --duration <seconds>    Test duration in seconds (default: 60)");
        Console.WriteLine("  --set-percent <0-100>   Percentage of SET operations (default: 30)");
        Console.WriteLine("  --get-percent <0-100>   Percentage of GET operations (default: 60)");
        Console.WriteLine("  --delete-percent <0-100> Percentage of DELETE operations (default: 10)");
        Console.WriteLine("  --hit-ratio <0-100>     Cache hit ratio for GETs (default: 75)");
        Console.WriteLine("  --min-size <bytes>      Minimum value size (default: 100)");
        Console.WriteLine("  --max-size <bytes>      Maximum value size (default: 1000)");
        Console.WriteLine("  --key-space <count>     Key space size (default: 100000)");
        Console.WriteLine("  --export <format>       Export format: json, csv, both (default: json)");
        Console.WriteLine("  --log <path>            Enable file logging to specified path");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MemCacheLoadTester list");
        Console.WriteLine("  MemCacheLoadTester scenario read-heavy --clients 50 --duration 120");
        Console.WriteLine("  MemCacheLoadTester custom --clients 20 --set-percent 50 --get-percent 45");
        Console.WriteLine("  MemCacheLoadTester scenario stress --log ./debug.log");
        
        return 0;
    }

    private static int ListScenarios()
    {
        Console.WriteLine("Available load test scenarios:");
        Console.WriteLine();
        
        var scenarios = LoadTestScenarios.GetScenarios();
        foreach (var scenario in scenarios.Keys.OrderBy(x => x))
        {
            var config = scenarios[scenario]();
            Console.WriteLine($"  {scenario,-15} - {config.Workload.SetPercentage}% SET, {config.Workload.GetPercentage}% GET, {config.Workload.DeletePercentage}% DELETE");
        }
        
        Console.WriteLine();
        Console.WriteLine("Usage: MemCacheLoadTester scenario <name> [options]");
        
        return 0;
    }

    private static async Task<int> RunScenario(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: Scenario name is required");
            Console.WriteLine("Usage: MemCacheLoadTester scenario <name> [options]");
            return 1;
        }

        var scenarioName = args[1];
        var scenarios = LoadTestScenarios.GetScenarios();
        
        if (!scenarios.ContainsKey(scenarioName))
        {
            Console.WriteLine($"Unknown scenario: {scenarioName}");
            Console.WriteLine("Available scenarios: " + string.Join(", ", scenarios.Keys));
            return 1;
        }

        var config = scenarios[scenarioName]();
        
        // Parse options
        for (int i = 2; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length) break;
            
            var option = args[i];
            var value = args[i + 1];
            
            switch (option.ToLower())
            {
                case "--host":
                    config.Host = value;
                    break;
                case "--port":
                    if (int.TryParse(value, out var port))
                        config.Port = port;
                    break;
                case "--clients":
                    if (int.TryParse(value, out var clients))
                        config.ConcurrentClients = clients;
                    break;
                case "--duration":
                    if (int.TryParse(value, out var duration))
                        config.DurationSeconds = duration;
                    break;
                case "--export":
                    ConfigureExport(config, value);
                    break;
                case "--log":
                    config.Reporting.EnableFileLogging = true;
                    config.Reporting.LogFilePath = value;
                    break;
            }
        }

        var loadTester = new LoadTester(config);
        await loadTester.RunAsync();
        return 0;
    }

    private static async Task<int> RunCustom(string[] args)
    {
        var config = new LoadTestConfig
        {
            Host = "localhost",
            Port = 11211,
            ConcurrentClients = 10,
            DurationSeconds = 60,
            RampUpSeconds = 6,
            
            Workload = new WorkloadConfig
            {
                SetPercentage = 30,
                GetPercentage = 60,
                DeletePercentage = 10,
                CacheHitRatio = 75
            },
            
            Keys = new KeyConfig
            {
                Prefix = "custom_",
                Pattern = KeyPattern.Sequential,
                KeySpaceSize = 100000,
                MaxLength = 200
            },
            
            Values = new ValueConfig
            {
                MinSizeBytes = 100,
                MaxSizeBytes = 1000,
                Pattern = ValuePattern.Random
            },
            
            Reporting = new ReportingConfig
            {
                ProgressIntervalSeconds = 10,
                OutputDirectory = "./results/custom",
                ExportJson = true,
                ExportCsv = false
            }
        };

        // Parse options
        for (int i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length) break;
            
            var option = args[i];
            var value = args[i + 1];
            
            switch (option.ToLower())
            {
                case "--host":
                    config.Host = value;
                    break;
                case "--port":
                    if (int.TryParse(value, out var port))
                        config.Port = port;
                    break;
                case "--clients":
                    if (int.TryParse(value, out var clients))
                        config.ConcurrentClients = clients;
                    break;
                case "--duration":
                    if (int.TryParse(value, out var duration))
                    {
                        config.DurationSeconds = duration;
                        config.RampUpSeconds = Math.Max(1, duration / 10);
                    }
                    break;
                case "--set-percent":
                    if (int.TryParse(value, out var setPercent))
                        config.Workload.SetPercentage = setPercent;
                    break;
                case "--get-percent":
                    if (int.TryParse(value, out var getPercent))
                        config.Workload.GetPercentage = getPercent;
                    break;
                case "--delete-percent":
                    if (int.TryParse(value, out var deletePercent))
                        config.Workload.DeletePercentage = deletePercent;
                    break;
                case "--hit-ratio":
                    if (int.TryParse(value, out var hitRatio))
                        config.Workload.CacheHitRatio = hitRatio;
                    break;
                case "--min-size":
                    if (int.TryParse(value, out var minSize))
                        config.Values.MinSizeBytes = minSize;
                    break;
                case "--max-size":
                    if (int.TryParse(value, out var maxSize))
                        config.Values.MaxSizeBytes = maxSize;
                    break;
                case "--key-space":
                    if (long.TryParse(value, out var keySpace))
                        config.Keys.KeySpaceSize = keySpace;
                    break;
                case "--export":
                    ConfigureExport(config, value);
                    break;
                case "--log":
                    config.Reporting.EnableFileLogging = true;
                    config.Reporting.LogFilePath = value;
                    break;
            }
        }

        var loadTester = new LoadTester(config);
        await loadTester.RunAsync();
        return 0;
    }

    private static void ConfigureExport(LoadTestConfig config, string format)
    {
        config.Reporting.ExportJson = false;
        config.Reporting.ExportCsv = false;

        switch (format.ToLower())
        {
            case "json":
                config.Reporting.ExportJson = true;
                break;
            case "csv":
                config.Reporting.ExportCsv = true;
                break;
            case "both":
                config.Reporting.ExportJson = true;
                config.Reporting.ExportCsv = true;
                break;
        }
    }
}