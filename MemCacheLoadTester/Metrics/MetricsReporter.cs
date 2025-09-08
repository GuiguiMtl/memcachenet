using System.Text;
using System.Text.Json;
using MemCacheLoadTester.Clients;
using MemCacheLoadTester.Configuration;

namespace MemCacheLoadTester.Metrics;

/// <summary>
/// Handles reporting and exporting of metrics data.
/// </summary>
public class MetricsReporter
{
    private readonly ReportingConfig _config;

    public MetricsReporter(ReportingConfig config)
    {
        _config = config;
        
        // Ensure output directory exists
        if (!Directory.Exists(_config.OutputDirectory))
        {
            Directory.CreateDirectory(_config.OutputDirectory);
        }
    }

    /// <summary>
    /// Prints a formatted progress report to the console.
    /// </summary>
    public void PrintProgressReport(MetricsSnapshot metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"  Load Test Progress - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        
        // Duration and throughput
        sb.AppendLine($"Test Duration:      {metrics.TestDurationSeconds:F1}s");
        sb.AppendLine($"Operations/sec:     {metrics.OperationsPerSecond:F1}");
        sb.AppendLine();
        
        // Operation counts
        sb.AppendLine("Operations:");
        sb.AppendLine($"  Total:            {metrics.TotalOperations:N0}");
        sb.AppendLine($"  Successful:       {metrics.SuccessfulOperations:N0} ({metrics.SuccessRate:F1}%)");
        sb.AppendLine($"  Failed:           {metrics.FailedOperations:N0}");
        sb.AppendLine();
        
        // Operation breakdown with success rates
        sb.AppendLine("Operation Types:");
        sb.AppendLine($"  SET:              {metrics.SetOperations:N0} ({metrics.SetSuccessful:N0} success, {metrics.SetFailed:N0} failed) - {metrics.SetSuccessRate:F1}%");
        sb.AppendLine($"  GET:              {metrics.GetOperations:N0} ({metrics.GetSuccessful:N0} success, {metrics.GetFailed:N0} failed) - {metrics.GetSuccessRate:F1}%");
        sb.AppendLine($"  DELETE:           {metrics.DeleteOperations:N0} ({metrics.DeleteSuccessful:N0} success, {metrics.DeleteFailed:N0} failed) - {metrics.DeleteSuccessRate:F1}%");
        sb.AppendLine();
        
        // Latency metrics
        sb.AppendLine("Latency (ms):");
        sb.AppendLine($"  Average:          {metrics.AverageLatencyMs:F2}");
        sb.AppendLine($"  Min:              {metrics.MinLatencyMs:F2}");
        sb.AppendLine($"  Max:              {metrics.MaxLatencyMs:F2}");
        sb.AppendLine($"  P50:              {metrics.P50LatencyMs:F2}");
        sb.AppendLine($"  P95:              {metrics.P95LatencyMs:F2}");
        sb.AppendLine($"  P99:              {metrics.P99LatencyMs:F2}");
        sb.AppendLine();
        
        // Errors
        if (metrics.FailedOperations > 0)
        {
            sb.AppendLine("Errors:");
            sb.AppendLine($"  Connection Errors: {metrics.ConnectionErrors:N0}");
            sb.AppendLine($"  Timeouts:         {metrics.Timeouts:N0}");
            sb.AppendLine();
        }
        
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        
        Console.Write(sb.ToString());
    }

    /// <summary>
    /// Prints a final summary report to the console.
    /// </summary>
    public void PrintFinalReport(MetricsSnapshot metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("████████████████████████████████████████████████████████████████");
        sb.AppendLine("                       FINAL LOAD TEST RESULTS");
        sb.AppendLine("████████████████████████████████████████████████████████████████");
        sb.AppendLine();
        
        // Key metrics
        sb.AppendLine("KEY METRICS:");
        sb.AppendLine($"  Test Duration:        {TimeSpan.FromSeconds(metrics.TestDurationSeconds):mm\\:ss}");
        sb.AppendLine($"  Total Operations:     {metrics.TotalOperations:N0}");
        sb.AppendLine($"  Average Throughput:   {metrics.OperationsPerSecond:F1} ops/sec");
        sb.AppendLine($"  Success Rate:         {metrics.SuccessRate:F1}%");
        sb.AppendLine();
        
        // Performance summary
        sb.AppendLine("PERFORMANCE SUMMARY:");
        sb.AppendLine($"  Average Latency:      {metrics.AverageLatencyMs:F2} ms");
        sb.AppendLine($"  95th Percentile:      {metrics.P95LatencyMs:F2} ms");
        sb.AppendLine($"  99th Percentile:      {metrics.P99LatencyMs:F2} ms");
        sb.AppendLine();
        
        // Operation breakdown
        var setPercent = metrics.TotalOperations > 0 ? (metrics.SetOperations / (double)metrics.TotalOperations) * 100 : 0;
        var getPercent = metrics.TotalOperations > 0 ? (metrics.GetOperations / (double)metrics.TotalOperations) * 100 : 0;
        var deletePercent = metrics.TotalOperations > 0 ? (metrics.DeleteOperations / (double)metrics.TotalOperations) * 100 : 0;
        
        sb.AppendLine("OPERATION BREAKDOWN:");
        sb.AppendLine($"  SET:                  {metrics.SetOperations:N0} ({setPercent:F1}%) - Success: {metrics.SetSuccessRate:F1}%");
        sb.AppendLine($"  GET:                  {metrics.GetOperations:N0} ({getPercent:F1}%) - Success: {metrics.GetSuccessRate:F1}%");
        sb.AppendLine($"  DELETE:               {metrics.DeleteOperations:N0} ({deletePercent:F1}%) - Success: {metrics.DeleteSuccessRate:F1}%");
        sb.AppendLine();
        
        if (metrics.FailedOperations > 0)
        {
            sb.AppendLine("ERROR SUMMARY:");
            sb.AppendLine($"  Failed Operations:    {metrics.FailedOperations:N0}");
            sb.AppendLine($"  Connection Errors:    {metrics.ConnectionErrors:N0}");
            sb.AppendLine($"  Timeouts:             {metrics.Timeouts:N0}");
            sb.AppendLine();
        }
        
        sb.AppendLine("████████████████████████████████████████████████████████████████");
        
        Console.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Exports metrics to JSON format.
    /// </summary>
    public async Task ExportToJsonAsync(MetricsSnapshot metrics, IEnumerable<OperationResult>? individualResults = null)
    {
        if (!_config.ExportJson) return;

        var exportData = new
        {
            Summary = metrics,
            IndividualResults = _config.IncludeIndividualResults ? individualResults : null,
            ExportTimestamp = DateTime.UtcNow,
            ConfigSettings = _config
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var filename = Path.Combine(_config.OutputDirectory, $"loadtest_results_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        await File.WriteAllTextAsync(filename, json);
        
        Console.WriteLine($"Results exported to: {filename}");
    }

    /// <summary>
    /// Exports metrics to CSV format.
    /// </summary>
    public async Task ExportToCsvAsync(MetricsSnapshot metrics, IEnumerable<OperationResult>? individualResults = null)
    {
        if (!_config.ExportCsv) return;

        // Export summary CSV
        var summaryFilename = Path.Combine(_config.OutputDirectory, $"loadtest_summary_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var summaryCsv = new StringBuilder();
        summaryCsv.AppendLine("Metric,Value,Unit");
        summaryCsv.AppendLine($"Test Duration,{metrics.TestDurationSeconds:F2},seconds");
        summaryCsv.AppendLine($"Total Operations,{metrics.TotalOperations},count");
        summaryCsv.AppendLine($"Successful Operations,{metrics.SuccessfulOperations},count");
        summaryCsv.AppendLine($"Failed Operations,{metrics.FailedOperations},count");
        summaryCsv.AppendLine($"Success Rate,{metrics.SuccessRate:F2},percent");
        summaryCsv.AppendLine($"Operations Per Second,{metrics.OperationsPerSecond:F2},ops/sec");
        summaryCsv.AppendLine($"Average Latency,{metrics.AverageLatencyMs:F2},milliseconds");
        summaryCsv.AppendLine($"Min Latency,{metrics.MinLatencyMs:F2},milliseconds");
        summaryCsv.AppendLine($"Max Latency,{metrics.MaxLatencyMs:F2},milliseconds");
        summaryCsv.AppendLine($"P50 Latency,{metrics.P50LatencyMs:F2},milliseconds");
        summaryCsv.AppendLine($"P95 Latency,{metrics.P95LatencyMs:F2},milliseconds");
        summaryCsv.AppendLine($"P99 Latency,{metrics.P99LatencyMs:F2},milliseconds");
        summaryCsv.AppendLine($"SET Operations,{metrics.SetOperations},count");
        summaryCsv.AppendLine($"GET Operations,{metrics.GetOperations},count");
        summaryCsv.AppendLine($"DELETE Operations,{metrics.DeleteOperations},count");
        summaryCsv.AppendLine($"Connection Errors,{metrics.ConnectionErrors},count");
        summaryCsv.AppendLine($"Timeouts,{metrics.Timeouts},count");

        await File.WriteAllTextAsync(summaryFilename, summaryCsv.ToString());
        Console.WriteLine($"Summary exported to: {summaryFilename}");

        // Export individual results if requested
        if (_config.IncludeIndividualResults && individualResults != null)
        {
            var detailsFilename = Path.Combine(_config.OutputDirectory, $"loadtest_details_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var detailsCsv = new StringBuilder();
            detailsCsv.AppendLine("Timestamp,Operation,Success,ElapsedMs,Response");
            
            foreach (var result in individualResults)
            {
                detailsCsv.AppendLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{result.Operation},{result.Success},{result.ElapsedMilliseconds:F2},\"{result.Response.Replace("\"", "\"\"")}\"");
            }
            
            await File.WriteAllTextAsync(detailsFilename, detailsCsv.ToString());
            Console.WriteLine($"Detailed results exported to: {detailsFilename}");
        }
    }
}