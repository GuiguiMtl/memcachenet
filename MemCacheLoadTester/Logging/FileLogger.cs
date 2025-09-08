using System.Collections.Concurrent;
using System.Text;

namespace MemCacheLoadTester.Logging;

/// <summary>
/// Simple file logger for the load tester to capture connection errors and valuable debugging information.
/// </summary>
public class FileLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StreamWriter _writer;
    private readonly ConcurrentQueue<string> _logQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _writeSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _loggingTask;
    private bool _disposed = false;

    public FileLogger(string logFilePath)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(_logFilePath, append: true, Encoding.UTF8) { AutoFlush = false };
        _logQueue = new ConcurrentQueue<string>();
        _writeSemaphore = new SemaphoreSlim(1, 1);
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Flush logs every 5 seconds
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        
        // Start background logging task
        _loggingTask = Task.Run(ProcessLogQueue, _cancellationTokenSource.Token);
        
        // Log session start
        LogInfo("=== Load Test Session Started ===");
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void LogInfo(string message)
    {
        Log("INFO", message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void LogWarning(string message)
    {
        Log("WARN", message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void LogError(string message)
    {
        Log("ERROR", message);
    }

    /// <summary>
    /// Logs an error with exception details.
    /// </summary>
    public void LogError(string message, Exception ex)
    {
        Log("ERROR", $"{message} - Exception: {ex.GetType().Name}: {ex.Message}");
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            Log("ERROR", $"Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Logs connection-related information.
    /// </summary>
    public void LogConnection(int clientId, string action, string details = "")
    {
        var message = $"Client-{clientId:D3}: {action}";
        if (!string.IsNullOrEmpty(details))
        {
            message += $" - {details}";
        }
        Log("CONN", message);
    }

    /// <summary>
    /// Logs operation results for debugging.
    /// </summary>
    public void LogOperation(int clientId, string operation, bool success, long elapsedMs, string details = "")
    {
        var status = success ? "SUCCESS" : "FAILED";
        var message = $"Client-{clientId:D3}: {operation} - {status} ({elapsedMs}ms)";
        if (!string.IsNullOrEmpty(details))
        {
            message += $" - {details}";
        }
        Log("OP", message);
    }

    /// <summary>
    /// Logs raw request/response communication for protocol debugging.
    /// </summary>
    public void LogRequestResponse(int clientId, string type, string data)
    {
        // Replace CRLF with visible markers for better readability
        var cleanData = data.Replace("\r\n", "[CRLF]").Replace("\r", "[CR]").Replace("\n", "[LF]");
        var message = $"Client-{clientId:D3}: {type} - {cleanData}";
        Log("PROTO", message);
    }

    /// <summary>
    /// Logs request/response communication with contextual information (operation, key, etc.).
    /// </summary>
    public void LogRequestResponseWithContext(int clientId, string operation, string key, string type, string data, string? additionalContext = null)
    {
        // Replace CRLF with visible markers for better readability
        var cleanData = data.Replace("\r\n", "[CRLF]").Replace("\r", "[CR]").Replace("\n", "[LF]");
        
        var context = $"[{operation}:{key}]";
        if (!string.IsNullOrEmpty(additionalContext))
        {
            context += $"[{additionalContext}]";
        }
        
        var message = $"Client-{clientId:D3}: {context} {type} - {cleanData}";
        Log("PROTO", message);
    }

    private void Log(string level, string message)
    {
        if (_disposed) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";
        _logQueue.Enqueue(logEntry);
    }

    private async Task ProcessLogQueue()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                if (_logQueue.TryDequeue(out var logEntry))
                {
                    await _writeSemaphore.WaitAsync(_cancellationTokenSource.Token);
                    try
                    {
                        await _writer.WriteLineAsync(logEntry);
                    }
                    finally
                    {
                        _writeSemaphore.Release();
                    }
                }
                else
                {
                    // No logs to process, wait a bit
                    await Task.Delay(50, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Can't log the logging error, just write to console
                Console.WriteLine($"Logging error: {ex.Message}");
            }
        }
    }

    private void FlushLogs(object? state)
    {
        if (_disposed) return;

        try
        {
            _writeSemaphore.Wait(TimeSpan.FromSeconds(1));
            try
            {
                _writer.Flush();
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error flushing logs: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            LogInfo("=== Load Test Session Ended ===");
            
            // Stop the flush timer
            _flushTimer?.Dispose();
            
            // Cancel the logging task
            _cancellationTokenSource.Cancel();
            
            // Wait for logging task to complete (with timeout)
            _loggingTask?.Wait(TimeSpan.FromSeconds(2));
            
            // Process any remaining logs
            while (_logQueue.TryDequeue(out var logEntry))
            {
                _writer.WriteLine(logEntry);
            }
            
            // Flush and close
            _writer.Flush();
            _writer.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing logger: {ex.Message}");
        }
        finally
        {
            _writer?.Dispose();
            _writeSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}