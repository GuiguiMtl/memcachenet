using System.Diagnostics;
using MemCacheLoadTester.Clients;
using MemCacheLoadTester.Configuration;
using MemCacheLoadTester.Logging;
using MemCacheLoadTester.Metrics;

namespace MemCacheLoadTester;

/// <summary>
/// Main orchestrator for load testing MemCache servers.
/// Manages multiple concurrent clients and coordinates the test execution.
/// </summary>
public class LoadTester
{
    private readonly LoadTestConfig _config;
    private readonly MetricsCollector _metrics;
    private readonly MetricsReporter _reporter;
    private readonly SharedKeyTracker _keyTracker;
    private readonly KeyGenerator _keyGenerator;
    private readonly ValueGenerator _valueGenerator;
    private readonly WorkloadSelector _workloadSelector;
    private readonly FileLogger? _logger;
    
    private readonly List<Task> _clientTasks;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly SemaphoreSlim _rampUpSemaphore;
    
    private Task? _progressReportingTask;

    public LoadTester(LoadTestConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metrics = new MetricsCollector();
        _reporter = new MetricsReporter(_config.Reporting);
        
        _keyTracker = new SharedKeyTracker();
        _keyGenerator = new KeyGenerator(_config.Keys, _keyTracker);
        _valueGenerator = new ValueGenerator(_config.Values);
        _workloadSelector = new WorkloadSelector(_config.Workload);
        
        // Initialize file logger if enabled
        _logger = _config.Reporting.EnableFileLogging 
            ? new FileLogger(_config.Reporting.LogFilePath) 
            : null;
        
        _clientTasks = new List<Task>();
        _cancellationTokenSource = new CancellationTokenSource();
        _rampUpSemaphore = new SemaphoreSlim(0);
        
        ValidateConfiguration();
    }

    /// <summary>
    /// Executes the load test according to the configuration.
    /// </summary>
    public async Task<MetricsSnapshot> RunAsync()
    {
        var startMessage = $"Starting load test with {_config.ConcurrentClients} clients...";
        var targetMessage = $"Target: {_config.Host}:{_config.Port}";
        var durationMessage = $"Duration: {_config.DurationSeconds}s, Ramp-up: {_config.RampUpSeconds}s";
        var workloadMessage = $"Workload: {_config.Workload.SetPercentage}% SET, {_config.Workload.GetPercentage}% GET, {_config.Workload.DeletePercentage}% DELETE";
        
        Console.WriteLine(startMessage);
        Console.WriteLine(targetMessage);
        Console.WriteLine(durationMessage);
        Console.WriteLine(workloadMessage);
        Console.WriteLine();

        _logger?.LogInfo(startMessage);
        _logger?.LogInfo(targetMessage);
        _logger?.LogInfo(durationMessage);
        _logger?.LogInfo(workloadMessage);

        try
        {
            // Start progress reporting
            _progressReportingTask = StartProgressReporting(_cancellationTokenSource.Token);

            // Start all client tasks
            await StartClientTasks();

            // Start ramp-up process
            await StartRampUp();

            // Wait for test duration or operation count
            await WaitForTestCompletion();

            Console.WriteLine("\nStopping load test...");
            
            // Stop all operations
            _cancellationTokenSource.Cancel();
            
            // Wait for all tasks to complete
            await Task.WhenAll(_clientTasks);
            
            // Stop progress reporting
            if (_progressReportingTask != null)
            {
                await _progressReportingTask;
            }

            // Get final metrics
            var finalMetrics = _metrics.GetSnapshot();
            
            // Print final report
            _reporter.PrintFinalReport(finalMetrics);
            
            // Export results
            await ExportResults(finalMetrics);
            
            return finalMetrics;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error during load test: {ex.Message}";
            Console.WriteLine(errorMessage);
            _logger?.LogError("Load test failed", ex);
            _cancellationTokenSource.Cancel();
            throw;
        }
        finally
        {
            _logger?.LogInfo("Load test completed");
            _logger?.Dispose();
            _rampUpSemaphore.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }

    /// <summary>
    /// Starts all client tasks but they wait for ramp-up signal.
    /// </summary>
    private async Task StartClientTasks()
    {
        for (int i = 0; i < _config.ConcurrentClients; i++)
        {
            var clientId = i;
            var task = Task.Run(() => RunClientAsync(clientId, _cancellationTokenSource.Token));
            _clientTasks.Add(task);
        }

        // Give tasks time to start
        await Task.Delay(100);
    }

    /// <summary>
    /// Gradually releases clients during ramp-up period.
    /// </summary>
    private async Task StartRampUp()
    {
        if (_config.RampUpSeconds <= 0)
        {
            // No ramp-up, release all clients immediately
            _rampUpSemaphore.Release(_config.ConcurrentClients);
            return;
        }

        var rampUpInterval = TimeSpan.FromMilliseconds((_config.RampUpSeconds * 1000.0) / _config.ConcurrentClients);
        
        Console.WriteLine($"Ramping up {_config.ConcurrentClients} clients over {_config.RampUpSeconds} seconds...");
        
        for (int i = 0; i < _config.ConcurrentClients; i++)
        {
            _rampUpSemaphore.Release(1);
            
            if (i < _config.ConcurrentClients - 1) // Don't delay after the last client
            {
                await Task.Delay(rampUpInterval, _cancellationTokenSource.Token);
            }
        }
        
        Console.WriteLine("Ramp-up complete!");
    }

    /// <summary>
    /// Waits for the test to complete based on duration or operation count.
    /// </summary>
    private async Task WaitForTestCompletion()
    {
        if (_config.DurationSeconds > 0)
        {
            // Wait for specified duration
            await Task.Delay(TimeSpan.FromSeconds(_config.DurationSeconds), _cancellationTokenSource.Token);
        }
        else if (_config.OperationCount > 0)
        {
            // Wait until operation count is reached
            while (_metrics.GetSnapshot().TotalOperations < _config.OperationCount && 
                   !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, _cancellationTokenSource.Token);
            }
        }
        else
        {
            // Run indefinitely (until Ctrl+C)
            Console.WriteLine("Running indefinitely. Press Ctrl+C to stop.");
            await Task.Delay(-1, _cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// Runs a single client's operation loop.
    /// </summary>
    private async Task RunClientAsync(int clientId, CancellationToken cancellationToken)
    {
        // Wait for ramp-up signal
        await _rampUpSemaphore.WaitAsync(cancellationToken);

        MemCacheClient? client = null;
        
        try
        {
            client = new MemCacheClient(_config.Host, _config.Port, _logger, clientId);
            _logger?.LogConnection(clientId, "Connected", $"to {_config.Host}:{_config.Port}");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var operation = _workloadSelector.SelectOperation();
                OperationResult result;
                
                switch (operation)
                {
                    case OperationType.Set:
                        result = await ExecuteSetOperation(client);
                        break;
                        
                    case OperationType.Get:
                        result = await ExecuteGetOperation(client);
                        break;
                        
                    case OperationType.Delete:
                        result = await ExecuteDeleteOperation(client);
                        break;
                        
                    default:
                        continue;
                }
                
                _metrics.RecordOperation(result);
                
                // Log failed operations for debugging
                if (!result.Success)
                {
                    _logger?.LogOperation(clientId, result.Operation, false, (long)result.ElapsedMilliseconds, result.Response);
                }
                
                // Small delay to prevent overwhelming the server
                await Task.Delay(1, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger?.LogConnection(clientId, "Cancelled", "Operation cancelled");
        }
        catch (Exception ex)
        {
            var errorResult = new OperationResult
            {
                Success = false,
                ElapsedMilliseconds = 0,
                Response = $"Client {clientId} error: {ex.Message}",
                Operation = "CLIENT_ERROR"
            };
            _metrics.RecordOperation(errorResult);
            _logger?.LogError($"Client {clientId} error", ex);
        }
        finally
        {
            if (client != null)
            {
                _logger?.LogConnection(clientId, "Disconnected", "Closing connection");
                client.Dispose();
            }
        }
    }

    /// <summary>
    /// Executes a SET operation.
    /// </summary>
    private async Task<OperationResult> ExecuteSetOperation(MemCacheClient client)
    {
        var key = _keyGenerator.GenerateKey();
        var value = _valueGenerator.GenerateValue();
        var result = await client.SetAsync(key, value);
        
        // Register the key if SET was successful
        if (result.Success)
        {
            _keyGenerator.RegisterStoredKey(key);
        }
        
        return result;
    }

    /// <summary>
    /// Executes a GET operation.
    /// </summary>
    private async Task<OperationResult> ExecuteGetOperation(MemCacheClient client)
    {
        string? key;
        
        if (_workloadSelector.ShouldCacheHit())
        {
            // Try to get a key from our local list of keys we've SET
            key = _keyGenerator.GenerateExistingKey();
            if (key == null)
            {
                // No keys in our local list yet, do a SET operation to build up our key list
                return await ExecuteSetOperation(client);
            }
        }
        else
        {
            // Intentional cache miss - generate a random key that probably doesn't exist
            key = _keyGenerator.GenerateKey();
        }
        
        return await client.GetAsync(key);
    }

    /// <summary>
    /// Executes a DELETE operation.
    /// </summary>
    private async Task<OperationResult> ExecuteDeleteOperation(MemCacheClient client)
    {
        // Try to get a key from our local list of keys we know exist
        var key = _keyGenerator.GenerateExistingKey();
        if (key == null)
        {
            // No keys in our local list yet, do a SET operation to build up our key list
            return await ExecuteSetOperation(client);
        }
        
        var result = await client.DeleteAsync(key);
        
        // Remove the key from our local list if DELETE was successful
        if (result.Success)
        {
            _keyGenerator.RemoveDeletedKey(key);
        }
        
        return result;
    }

    /// <summary>
    /// Starts the progress reporting task.
    /// </summary>
    private Task StartProgressReporting(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var interval = TimeSpan.FromSeconds(_config.Reporting.ProgressIntervalSeconds);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);
                    
                    var metrics = _metrics.GetSnapshot();
                    _reporter.PrintProgressReport(metrics);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Exports test results to configured formats.
    /// </summary>
    private async Task ExportResults(MetricsSnapshot metrics)
    {
        try
        {
            var individualResults = _config.Reporting.IncludeIndividualResults 
                ? _metrics.GetAllResults() 
                : null;

            if (_config.Reporting.ExportJson)
            {
                await _reporter.ExportToJsonAsync(metrics, individualResults);
            }

            if (_config.Reporting.ExportCsv)
            {
                await _reporter.ExportToCsvAsync(metrics, individualResults);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to export results: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the configuration for common issues.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (_config.ConcurrentClients <= 0)
            throw new ArgumentException("ConcurrentClients must be greater than 0");
        
        if (_config.DurationSeconds <= 0 && _config.OperationCount <= 0)
            throw new ArgumentException("Either DurationSeconds or OperationCount must be greater than 0");
        
        _config.Workload.Validate();
        
        if (string.IsNullOrWhiteSpace(_config.Host))
            throw new ArgumentException("Host cannot be null or empty");
        
        if (_config.Port <= 0 || _config.Port > 65535)
            throw new ArgumentException("Port must be between 1 and 65535");
    }
}