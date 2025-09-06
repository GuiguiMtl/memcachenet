using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

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

    /// <summary>
    /// Initializes a new MemCache client connection.
    /// </summary>
    /// <param name="host">Server hostname or IP address</param>
    /// <param name="port">Server port (default 11211)</param>
    public MemCacheClient(string host = "localhost", int port = 11211)
    {
        _tcpClient = new TcpClient();
        _tcpClient.Connect(host, port);
        _stream = _tcpClient.GetStream();
        _buffer = new byte[64 * 1024]; // 64KB buffer for responses
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
            
            // Send command header
            await _stream.WriteAsync(commandBytes);
            
            // Send data
            await _stream.WriteAsync(value);
            
            // Send trailing CRLF
            await _stream.WriteAsync("\r\n"u8.ToArray());

            // Read response
            var response = await ReadResponseAsync();
            sw.Stop();

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
            
            await _stream.WriteAsync(commandBytes);
            
            var response = await ReadResponseAsync();
            sw.Stop();

            // Check if we got a value back (not "END" only)
            var success = response.StartsWith($"VALUE {key}");
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
            
            await _stream.WriteAsync(commandBytes);
            
            var response = await ReadResponseAsync();
            sw.Stop();

            var success = response.StartsWith("DELETED");
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
    /// Reads a complete response from the server.
    /// Handles both single-line responses (STORED, DELETED, etc.) and multi-line responses (GET).
    /// </summary>
    private async Task<string> ReadResponseAsync()
    {
        var responseBuilder = new StringBuilder();
        var totalBytesRead = 0;
        
        while (true)
        {
            var bytesRead = await _stream.ReadAsync(_buffer);
            if (bytesRead == 0)
                break;
                
            var chunk = Encoding.UTF8.GetString(_buffer, 0, bytesRead);
            responseBuilder.Append(chunk);
            totalBytesRead += bytesRead;
            
            var currentResponse = responseBuilder.ToString();
            
            // Check for complete response patterns
            if (currentResponse.EndsWith("\r\nEND\r\n") ||     // GET response
                currentResponse.EndsWith("STORED\r\n") ||      // SET success
                currentResponse.EndsWith("DELETED\r\n") ||     // DELETE success
                currentResponse.EndsWith("NOT_FOUND\r\n") ||   // GET/DELETE not found
                currentResponse.Contains("ERROR") ||           // Any error
                currentResponse.Contains("CLIENT_ERROR") ||    // Client error
                currentResponse.Contains("SERVER_ERROR"))      // Server error
            {
                break;
            }
            
            // Safety check to prevent infinite reading
            if (totalBytesRead > _buffer.Length)
                break;
        }
        
        return responseBuilder.ToString().Trim();
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