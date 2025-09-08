using System.Collections.Concurrent;
using System.Diagnostics;
using MemCacheLoadTester.Clients;

namespace MemCacheLoadTester.Metrics;

/// <summary>
/// Thread-safe metrics collector for load testing operations.
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentQueue<OperationResult> _results;
    private readonly object _lock = new();
    
    // Operation counters
    private long _totalOperations;
    private long _successfulOperations;
    private long _failedOperations;
    
    // Operation type counters
    private long _setOperations;
    private long _getOperations;
    private long _deleteOperations;
    
    // Per-operation success counters
    private long _setSuccessful;
    private long _getSuccessful;
    private long _deleteSuccessful;
    
    // Per-operation failure counters
    private long _setFailed;
    private long _getFailed;
    private long _deleteFailed;
    
    // Timing accumulators
    private double _totalLatencyMs;
    private double _minLatencyMs = double.MaxValue;
    private double _maxLatencyMs = double.MinValue;
    
    // Percentile calculation data
    private readonly List<double> _latencies;
    
    // Connection metrics
    private long _connectionErrors;
    private long _timeouts;
    
    private readonly Stopwatch _testDuration;
    
    public MetricsCollector()
    {
        _results = new ConcurrentQueue<OperationResult>();
        _latencies = new List<double>();
        _testDuration = Stopwatch.StartNew();
    }

    /// <summary>
    /// Records an operation result.
    /// </summary>
    public void RecordOperation(OperationResult result)
    {
        _results.Enqueue(result);
        
        lock (_lock)
        {
            _totalOperations++;
            
            if (result.Success)
            {
                _successfulOperations++;
            }
            else
            {
                _failedOperations++;
                
                // Categorize errors
                if (result.Response.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    _timeouts++;
                }
                else if (result.Response.Contains("connection", StringComparison.OrdinalIgnoreCase))
                {
                    _connectionErrors++;
                }
            }
            
            // Update operation type counters and per-operation success/failure
            switch (result.Operation.ToUpper())
            {
                case "SET":
                    _setOperations++;
                    if (result.Success)
                        _setSuccessful++;
                    else
                        _setFailed++;
                    break;
                case "GET":
                    _getOperations++;
                    if (result.Success)
                        _getSuccessful++;
                    else
                        _getFailed++;
                    break;
                case "DELETE":
                    _deleteOperations++;
                    if (result.Success)
                        _deleteSuccessful++;
                    else
                        _deleteFailed++;
                    break;
            }
            
            // Update latency metrics
            _totalLatencyMs += result.ElapsedMilliseconds;
            _latencies.Add(result.ElapsedMilliseconds);
            
            if (result.ElapsedMilliseconds < _minLatencyMs)
                _minLatencyMs = result.ElapsedMilliseconds;
            
            if (result.ElapsedMilliseconds > _maxLatencyMs)
                _maxLatencyMs = result.ElapsedMilliseconds;
        }
    }

    /// <summary>
    /// Gets a snapshot of current metrics.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var elapsedSeconds = _testDuration.Elapsed.TotalSeconds;
            var throughput = elapsedSeconds > 0 ? _totalOperations / elapsedSeconds : 0;
            
            return new MetricsSnapshot
            {
                // Basic counters
                TotalOperations = _totalOperations,
                SuccessfulOperations = _successfulOperations,
                FailedOperations = _failedOperations,
                
                // Operation type breakdown
                SetOperations = _setOperations,
                GetOperations = _getOperations,
                DeleteOperations = _deleteOperations,
                
                // Per-operation success counts
                SetSuccessful = _setSuccessful,
                GetSuccessful = _getSuccessful,
                DeleteSuccessful = _deleteSuccessful,
                
                // Per-operation failure counts
                SetFailed = _setFailed,
                GetFailed = _getFailed,
                DeleteFailed = _deleteFailed,
                
                // Per-operation success rates
                SetSuccessRate = _setOperations > 0 ? (_setSuccessful / (double)_setOperations) * 100 : 0,
                GetSuccessRate = _getOperations > 0 ? (_getSuccessful / (double)_getOperations) * 100 : 0,
                DeleteSuccessRate = _deleteOperations > 0 ? (_deleteSuccessful / (double)_deleteOperations) * 100 : 0,
                
                // Success rate
                SuccessRate = _totalOperations > 0 ? (_successfulOperations / (double)_totalOperations) * 100 : 0,
                
                // Throughput
                OperationsPerSecond = throughput,
                
                // Latency metrics
                AverageLatencyMs = _totalOperations > 0 ? _totalLatencyMs / _totalOperations : 0,
                MinLatencyMs = _minLatencyMs == double.MaxValue ? 0 : _minLatencyMs,
                MaxLatencyMs = _maxLatencyMs == double.MinValue ? 0 : _maxLatencyMs,
                
                // Percentiles
                P50LatencyMs = CalculatePercentile(50),
                P95LatencyMs = CalculatePercentile(95),
                P99LatencyMs = CalculatePercentile(99),
                
                // Error metrics
                ConnectionErrors = _connectionErrors,
                Timeouts = _timeouts,
                
                // Test duration
                TestDurationSeconds = elapsedSeconds,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Gets all individual operation results (for detailed analysis).
    /// </summary>
    public IEnumerable<OperationResult> GetAllResults()
    {
        var results = new List<OperationResult>();
        while (_results.TryDequeue(out var result))
        {
            results.Add(result);
        }
        return results;
    }

    /// <summary>
    /// Calculates the specified percentile for latency.
    /// </summary>
    private double CalculatePercentile(double percentile)
    {
        if (_latencies.Count == 0)
            return 0;
            
        var sorted = _latencies.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling((percentile / 100.0) * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }

    /// <summary>
    /// Resets all metrics (for test restart scenarios).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            while (_results.TryDequeue(out _)) { }
            
            _totalOperations = 0;
            _successfulOperations = 0;
            _failedOperations = 0;
            _setOperations = 0;
            _getOperations = 0;
            _deleteOperations = 0;
            _setSuccessful = 0;
            _getSuccessful = 0;
            _deleteSuccessful = 0;
            _setFailed = 0;
            _getFailed = 0;
            _deleteFailed = 0;
            _totalLatencyMs = 0;
            _minLatencyMs = double.MaxValue;
            _maxLatencyMs = double.MinValue;
            _connectionErrors = 0;
            _timeouts = 0;
            
            _latencies.Clear();
            _testDuration.Restart();
        }
    }
}

/// <summary>
/// Immutable snapshot of metrics at a point in time.
/// </summary>
public class MetricsSnapshot
{
    // Basic counters
    public long TotalOperations { get; set; }
    public long SuccessfulOperations { get; set; }
    public long FailedOperations { get; set; }
    
    // Operation type breakdown
    public long SetOperations { get; set; }
    public long GetOperations { get; set; }
    public long DeleteOperations { get; set; }
    
    // Per-operation success counts
    public long SetSuccessful { get; set; }
    public long GetSuccessful { get; set; }
    public long DeleteSuccessful { get; set; }
    
    // Per-operation failure counts
    public long SetFailed { get; set; }
    public long GetFailed { get; set; }
    public long DeleteFailed { get; set; }
    
    // Per-operation success rates
    public double SetSuccessRate { get; set; }
    public double GetSuccessRate { get; set; }
    public double DeleteSuccessRate { get; set; }
    
    // Rates and throughput
    public double SuccessRate { get; set; }
    public double OperationsPerSecond { get; set; }
    
    // Latency metrics
    public double AverageLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    
    // Error metrics
    public long ConnectionErrors { get; set; }
    public long Timeouts { get; set; }
    
    // Test metadata
    public double TestDurationSeconds { get; set; }
    public DateTime Timestamp { get; set; }
}