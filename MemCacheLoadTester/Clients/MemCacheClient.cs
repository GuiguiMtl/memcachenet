using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using MemCacheLoadTester.Logging;

namespace MemCacheLoadTester.Clients;

/// <summary>
/// High-performance MemCache client that implements the memcached text protocol.
/// Designed for load testing with precise timing measurements.
/// </summary>
public class MemCacheClient : IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly byte[] _buffer;
    private bool _disposed;
    private FileLogger? _logger;
    private int _clientId;

    /// <summary>
    /// Initializes a new MemCache client connection.
    /// </summary>
    /// <param name="host">Server hostname or IP address</param>
    /// <param name="port">Server port (default 11211)</param>
    /// <param name="logger">Optional logger for request/response logging</param>
    /// <param name="clientId">Client ID for logging purposes</param>
    public MemCacheClient(string host = "localhost", int port = 11211, FileLogger? logger = null, int clientId = 0)
    {
        _tcpClient = new TcpClient();
        _tcpClient.Connect(host, port);
        _stream = _tcpClient.GetStream();
        _buffer = new byte[64 * 1024]; // 64KB buffer for responses
        _logger = logger;
        _clientId = clientId;
    }

    /// <summary>
    /// Performs a SET operation and measures the latency.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to store</param>
    /// <param name="flags">Optional flags</param>
    /// <param name="expiration">Expiration time in seconds</param>
    /// <returns>Operation result with timing information</returns>
    public async Task<OperationResult> SetAsync(string key, byte[] value, uint flags = 0, uint expiration = 0)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var command = $"set {key} {flags} {expiration} {value.Length}\r\n";
            var commandBytes = Encoding.UTF8.GetBytes(command);
            
            // Log the request with context
            var valueInfo = value.Length <= 100 ? $" + [{Encoding.UTF8.GetString(value)}]" : $" + [binary data: {value.Length} bytes]";
            _logger?.LogRequestResponseWithContext(_clientId, "SET", key, "REQUEST", $"{command.TrimEnd()}{valueInfo}", $"flags:{flags},exp:{expiration},size:{value.Length}");
            
            // Send command header
            await _stream.WriteAsync(commandBytes);
            
            // Send data
            await _stream.WriteAsync(value);
            
            // Send trailing CRLF
            await _stream.WriteAsync("\r\n"u8.ToArray());

            // Read response
            var response = await ReadResponseAsync("SET", key);
            sw.Stop();

            // Log the response with context
            _logger?.LogRequestResponseWithContext(_clientId, "SET", key, "RESPONSE", response, $"latency:{sw.Elapsed.TotalMilliseconds:F2}ms");

            var success = response.StartsWith("STORED");
            return new OperationResult
            {
                Success = success,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Response = response,
                Operation = "SET"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogRequestResponseWithContext(_clientId, "SET", key, "ERROR", $"SET operation failed: {ex.Message}", $"latency:{sw.Elapsed.TotalMilliseconds:F2}ms");
            return new OperationResult
            {
                Success = false,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Response = ex.Message,
                Operation = "SET"
            };
        }
    }

    /// <summary>
    /// Performs a GET operation and measures the latency.
    /// </summary>
    /// <param name="key">Cache key to retrieve</param>
    /// <returns>Operation result with timing information</returns>
    public async Task<OperationResult> GetAsync(string key)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var command = $"get {key}\r\n";
            var commandBytes = Encoding.UTF8.GetBytes(command);
            
            // Log the request with context
            _logger?.LogRequestResponseWithContext(_clientId, "GET", key, "REQUEST", command.TrimEnd());
            
            await _stream.WriteAsync(commandBytes);
            
            var response = await ReadResponseAsync("GET", key);
            sw.Stop();

            // Determine if this is a cache hit or miss, both are successful GET operations
            var isHit = response.StartsWith($"VALUE {key}");
            var isMiss = response.Trim() == "END";
            var success = isHit || isMiss;  // Both hits and misses are successful GET operations
            
            var logResponse = response.Length > 200 ? response.Substring(0, 200) + "... (truncated)" : response;
            var contextInfo = isHit ? "HIT" : (isMiss ? "MISS" : "ERROR");
            _logger?.LogRequestResponseWithContext(_clientId, "GET", key, "RESPONSE", logResponse, $"{contextInfo},latency:{sw.Elapsed.TotalMilliseconds:F2}ms");
            return new OperationResult
            {
                Success = success,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Response = response,
                Operation = "GET"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogRequestResponseWithContext(_clientId, "GET", key, "ERROR", $"GET operation failed: {ex.Message}", $"latency:{sw.Elapsed.TotalMilliseconds:F2}ms");
            return new OperationResult
            {
                Success = false,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Response = ex.Message,
                Operation = "GET"
            };
        }
    }

    /// <summary>
    /// Performs a DELETE operation and measures the latency.
    /// </summary>
    /// <param name="key">Cache key to delete</param>
    /// <returns>Operation result with timing information</returns>
    public async Task<OperationResult> DeleteAsync(string key)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var command = $"delete {key}\r\n";
            var commandBytes = Encoding.UTF8.GetBytes(command);
            
            // Log the request with context
            _logger?.LogRequestResponseWithContext(_clientId, "DELETE", key, "REQUEST", command.TrimEnd());
            
            await _stream.WriteAsync(commandBytes);
            
            var response = await ReadResponseAsync("DELETE", key);
            sw.Stop();

            // Check success and log the response with context
            var success = response.StartsWith("DELETED");
            var contextInfo = success ? "DELETED" : "NOT_FOUND";
            _logger?.LogRequestResponseWithContext(_clientId, "DELETE", key, "RESPONSE", response, $"{contextInfo},latency:{sw.Elapsed.TotalMilliseconds:F2}ms");
            return new OperationResult
            {
                Success = success,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Response = response,
                Operation = "DELETE"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogRequestResponseWithContext(_clientId, "DELETE", key, "ERROR", $"DELETE operation failed: {ex.Message}", $"latency:{sw.Elapsed.TotalMilliseconds:F2}ms");
            return new OperationResult
            {
                Success = false,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Response = ex.Message,
                Operation = "DELETE"
            };
        }
    }

    /// <summary>
    /// Reads a response from the server with a simple timeout-based approach.
    /// Just reads what's available and logs it for debugging.
    /// </summary>
    private async Task<string> ReadResponseAsync(string operation = "UNKNOWN", string key = "unknown")
    {
        var responseBuilder = new StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12)); // 5 second timeout
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // Check if data is available
                if (_stream.DataAvailable)
                {
                    var bytesRead = await _stream.ReadAsync(_buffer, cts.Token);
                    if (bytesRead == 0)
                        break;
                        
                    var chunk = Encoding.UTF8.GetString(_buffer, 0, bytesRead);
                    responseBuilder.Append(chunk);
                    
                    // Log what we received for debugging with context
                    _logger?.LogRequestResponseWithContext(_clientId, operation, key, "RAW_DATA", $"Received {bytesRead} bytes: {chunk.Replace("\r", "[CR]").Replace("\n", "[LF]")}");
                    
                    // Simple heuristic: if we see common end patterns, we're probably done
                    var current = responseBuilder.ToString();
                    if (current.Contains("STORED") || current.Contains("DELETED") || 
                        current.Contains("NOT_FOUND") || current.Contains("END") ||
                        current.Contains("ERROR"))
                    {
                        // Give a small delay to catch any remaining data
                        await Task.Delay(10, cts.Token);
                        if (_stream.DataAvailable)
                        {
                            continue; // More data coming
                        }
                        break;
                    }
                }
                else
                {
                    // No data available, wait a bit
                    await Task.Delay(50, cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogRequestResponseWithContext(_clientId, operation, key, "TIMEOUT", "Response reading timed out");
        }
        
        var response = responseBuilder.ToString();
        _logger?.LogRequestResponseWithContext(_clientId, operation, key, "FINAL_RESPONSE", $"Complete response ({response.Length} chars): {response.Replace("\r", "[CR]").Replace("\n", "[LF]")}");
        
        return response;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents the result of a memcache operation with timing information.
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public double ElapsedMilliseconds { get; set; }
    public string Response { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}