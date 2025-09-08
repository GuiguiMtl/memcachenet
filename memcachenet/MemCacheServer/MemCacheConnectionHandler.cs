using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Handles TCP client connections for the MemCache server, managing data reading and writing using pipelines.
/// </summary>
/// <param name="client">The TCP client connection to handle.</param>
/// <param name="onLineRead">Callback invoked when a complete line is read from the client, with a response writer function.</param>
/// <param name="settings">Server settings including timeout configuration.</param>
public class MemCacheConnectionHandler(
    TcpClient client,
    Action<ReadOnlySequence<byte>, Func<byte[], Task>>? onLineRead,
    MemCacheServerSettings settings,
    ILogger<MemCacheConnectionHandler>? logger = null)
    : IDisposable
{
    /// <summary>
    /// The TCP client connection being handled.
    /// </summary>
    private readonly TcpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    
    /// <summary>
    /// Logger for debugging connection handling.
    /// </summary>
    private readonly ILogger<MemCacheConnectionHandler>? _logger = logger;

    /// <summary>
    /// Server settings including timeout configuration.
    /// </summary>
    private readonly MemCacheServerSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    
    /// <summary>
    /// The network stream for reading from and writing to the client.
    /// </summary>
    private NetworkStream? _stream;
    
    /// <summary>
    /// Connection identifier for tracing purposes.
    /// </summary>
    private readonly string _connectionId = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Cancellation token source for connection timeouts.
    /// </summary>
    private readonly CancellationTokenSource _connectionCts = new();

    /// <summary>
    /// Timer for tracking idle connection timeout.
    /// </summary>
    private Timer? _idleTimer;

    /// <summary>
    /// Lock for thread-safe timer operations.
    /// </summary>
    private readonly object _timerLock = new();

    /// <summary>
    /// Handles the client connection asynchronously by setting up reading and writing pipelines.
    /// </summary>
    /// <returns>A task representing the asynchronous connection handling operation.</returns>
    public async Task HandleConnectionAsync(CancellationToken token)
    {
        // Combine external cancellation token with connection timeout token
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _connectionCts.Token);
        var combinedToken = combinedCts.Token;
        using (_logger?.BeginScope(new Dictionary<string, object>
        {
            ["ConnectionId"] = _connectionId,
        }))
        {
            try
            {

                _logger?.LogDebug("Starting connection handling from {ClientEndpoint}",
                    _client.Client.RemoteEndPoint?.ToString() ?? "unknown");

                _stream = _client.GetStream();

                // Initialize idle timer if timeout is configured
                if (_settings.ConnectionIdleTimeoutSeconds > 0)
                {
                    StartIdleTimer();
                }

                var pipe = new Pipe();
                Task writing = FillPipeAsync(combinedToken, _stream, pipe.Writer);
                Task reading = ReadPipeAsync(combinedToken, pipe.Reader);

                await Task.WhenAll(reading, writing);

                _logger?.LogDebug("Connection handling completed");

            }
            catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
            {
                if (_connectionCts.Token.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    _logger?.LogInformation("Connection timed out");
                }
                else
                {
                    _logger?.LogInformation("Connection cancelled");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling connection {ConnectionId}: {ErrorMessage}", _connectionId, ex.Message);
                throw;
            }
            finally
            {
                StopIdleTimer();
                _client.Close();
            }
        }
    }

    /// <summary>
    /// Continuously reads data from the network stream and writes it to the pipe writer.
    /// </summary>
    /// <param name="stream">The network stream to read from.</param>
    /// <param name="writer">The pipe writer to write data to.</param>
    /// <returns>A task representing the asynchronous fill operation.</returns>
    private async Task FillPipeAsync(CancellationToken token, NetworkStream stream, PipeWriter writer)
    {
        using var activity = MemCacheTelemetry.ActivitySource.StartActivity(MemCacheTelemetry.ActivityNames.PipelineFill);
        activity?.SetTag(MemCacheTelemetry.Tags.ConnectionId, _connectionId);
        
        const int minimumBufferSize = 512;
        var totalBytesRead = 0;

        try
        {
            while (true && !token.IsCancellationRequested)
            {
                // Allocate at least 512 bytes from the PipeWriter.
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                
                int bytesRead = await stream.ReadAsync(memory, token);
                if (bytesRead == 0)
                {
                    break;
                }
                
                totalBytesRead += bytesRead;
                writer.Advance(bytesRead);

                // Make the data available to the PipeReader.
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag(MemCacheTelemetry.Tags.ErrorType, ex.GetType().Name);
        }
        finally
        {
            activity?.SetTag(MemCacheTelemetry.Tags.DataSize, totalBytesRead);
            // By completing PipeWriter, tell the PipeReader that there's no more data coming.
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Continuously reads data from the pipe reader and processes complete lines.
    /// </summary>
    /// <param name="reader">The pipe reader to read from.</param>
    /// <returns>A task representing the asynchronous read operation.</returns>
    private async Task ReadPipeAsync(CancellationToken token, PipeReader reader)
    {
        using var activity = MemCacheTelemetry.ActivitySource.StartActivity(MemCacheTelemetry.ActivityNames.PipelineRead);
        activity?.SetTag(MemCacheTelemetry.Tags.ConnectionId, _connectionId);
        
        var commandCount = 0;
        
        try
        {
            _logger?.LogTrace("Starting to read pipe for connection {ConnectionId}", _connectionId);
            
            ReadOnlySequence<byte> buffer = default;
            
            while (true && !token.IsCancellationRequested)
            {
                ReadResult result;
                
                // Apply read timeout only if we have partial data (incomplete command)
                bool hasPartialData = buffer.Length > 0;
                
                if (hasPartialData && _settings.ReadTimeoutSeconds > 0)
                {
                    using var readTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.ReadTimeoutSeconds));
                    using var combinedReadCts = CancellationTokenSource.CreateLinkedTokenSource(token, readTimeoutCts.Token);
                    
                    try
                    {
                        result = await reader.ReadAsync(combinedReadCts.Token);
                    }
                    catch (OperationCanceledException) when (readTimeoutCts.Token.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        _logger?.LogWarning("Read timeout reached for connection {ConnectionId}", _connectionId);
                        _connectionCts.Cancel(); // Trigger connection timeout
                        throw;
                    }
                }
                else
                {
                    // No partial data, wait indefinitely for new command
                    result = await reader.ReadAsync(token);
                }
                
                buffer = result.Buffer;

                while (TryReadMemCacheCommand(ref buffer, out ReadOnlySequence<byte> command))
                {
                    commandCount++;
                    using var commandActivity = MemCacheTelemetry.ActivitySource.StartActivity(MemCacheTelemetry.ActivityNames.CommandRead);
                    commandActivity?.SetTag(MemCacheTelemetry.Tags.ConnectionId, _connectionId);
                    commandActivity?.SetTag(MemCacheTelemetry.Tags.DataSize, command.Length);
                    
                    _logger?.LogDebug("Processing command {CommandNumber} for connection {ConnectionId}, size: {CommandSize} bytes", 
                        commandCount, _connectionId, command.Length);
                    
                    // Reset idle timer when command is received
                    ResetIdleTimer();
                    
                    // invoke the callback when a command is read
                    onLineRead?.Invoke(command, WriteResponseAsync);
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    _logger?.LogTrace("Pipe read completed for connection {ConnectionId}", _connectionId);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading pipe for connection {ConnectionId}: {ErrorMessage}", _connectionId, ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag(MemCacheTelemetry.Tags.ErrorType, ex.GetType().Name);
        }
        finally
        {
            _logger?.LogDebug("Finished processing {CommandCount} commands for connection {ConnectionId}", commandCount, _connectionId);
            activity?.SetTag("memcache.commands.processed", commandCount);
            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
        }
    }
    
    /// <summary>
    /// Attempts to read a complete line ending with CRLF (\r\n) from the buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read from, modified to exclude the processed line.</param>
    /// <param name="line">The output line including the CRLF terminator.</param>
    /// <returns>True if a complete line was found; otherwise, false.</returns>
    bool TryReadMemCacheCommand(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> command)
    {
        command = default;
        
        // First, try to find a complete command line (ending with \r\n)
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadTo(out ReadOnlySequence<byte> commandLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            // Check if we have data that might be malformed (contains \r or \n but not \r\n)
            if (TryDetectMalformedCommand(buffer, out command))
            {
                // Advance the buffer past the malformed command
                buffer = buffer.Slice(command.Length);
                return true; // Return malformed command for error handling
            }
            return false; // No complete command line found
        }
        
        // Check if this is a SET command that needs special handling
        var commandLineReader = new SequenceReader<byte>(commandLine);
        if (commandLineReader.TryReadTo(out ReadOnlySpan<byte> commandSpan, (byte)' '))
        {
            var commandText = System.Text.Encoding.UTF8.GetString(commandSpan);
            if (commandText.Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                // This is a SET command - we need to read the data block too
                return TryReadSetCommand(ref buffer, out command);
            }
        }
        
        // For non-SET commands (GET, DELETE, etc.), just return the command line
        var commandLineWithCrlf = buffer.Slice(0, reader.Position);
        command = commandLineWithCrlf;
        buffer = buffer.Slice(reader.Position);
        return true;
    }

    /// <summary>
    /// Detects malformed commands that have improper line endings (\r only or \n only)
    /// </summary>
    private bool TryDetectMalformedCommand(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> command)
    {
        command = default;
        var reader = new SequenceReader<byte>(buffer);
        
        // Look for lone \r (not followed by \n)
        while (reader.TryReadTo(out ReadOnlySequence<byte> lineBeforeCr, (byte)'\r'))
        {
            if (reader.End || (reader.IsNext((byte)'\n') == false))
            {
                // Found \r not followed by \n - this is malformed
                var malformedLength = lineBeforeCr.Length + 1; // Include the \r
                if (reader.Remaining > 0 && !reader.IsNext((byte)'\n'))
                {
                    // Include additional content until proper termination or buffer end
                    var remainingReader = new SequenceReader<byte>(reader.UnreadSequence);
                    byte nextByte;
                    while (remainingReader.TryRead(out nextByte) && nextByte != (byte)'\n')
                    {
                        malformedLength++;
                    }
                    if (remainingReader.Consumed > 0 && nextByte == (byte)'\n') 
                    {
                        malformedLength++; // Include the \n if found
                    }
                }
                command = buffer.Slice(0, malformedLength);
                return true;
            }
        }
        
        // Reset reader to check for lone \n
        reader = new SequenceReader<byte>(buffer);
        
        // Look for \n not preceded by \r
        var position = 0L;
        while (reader.TryRead(out byte currentByte))
        {
            if (currentByte == (byte)'\n')
            {
                // Check if previous byte was \r
                if (position == 0 || buffer.Slice(position - 1, 1).FirstSpan[0] != (byte)'\r')
                {
                    // Found \n not preceded by \r - this is malformed
                    command = buffer.Slice(0, position + 1); // Include the \n
                    return true;
                }
            }
            position++;
        }
        
        return false;
    }

    bool TryReadSetCommand(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> command)
    {
        command = default;
        var reader = new SequenceReader<byte>(buffer);
        
        // Read the command line (already validated to exist)
        if (!reader.TryReadTo(out ReadOnlySequence<byte> commandLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            return false;
        }
        
        // Parse the command line to extract the data length
        var dataLength = ExtractDataLengthFromSetCommand(commandLine);
        if (dataLength < 0)
        {
            // Invalid data length - pass the command line to parser for proper error handling
            command = buffer.Slice(0, reader.Position);
            buffer = buffer.Slice(reader.Position);
            return true;
        }
        
        // Check if we have enough data for the complete command (data + \r\n)
        if (reader.Remaining < dataLength + 2)
        {
            // Check if we have any data block that ends with \r\n
            // This handles cases where declared length doesn't match actual data
            var remainingBuffer = reader.UnreadSequence;
            var tempReader = new SequenceReader<byte>(remainingBuffer);
            if (tempReader.TryReadTo(out ReadOnlySequence<byte> _, new byte[] { (byte)'\r', (byte)'\n' }))
            {
                // We found a \r\n, so pass this malformed command to parser for proper error handling
                var totalConsumed = reader.Consumed + tempReader.Consumed;
                command = buffer.Slice(0, totalConsumed);
                buffer = buffer.Slice(totalConsumed);
                return true;
            }
            
            return false; // Not enough data available yet
        }
        
        // Advance reader past the data block and its trailing \r\n
        reader.Advance(dataLength);
        if (reader.Remaining >= 2 && reader.UnreadSpan[0] == (byte)'\r' && reader.UnreadSpan[1] == (byte)'\n')
        {
            reader.Advance(2);
        }
        
        // Return the complete command from start to current reader position
        command = buffer.Slice(0, reader.Position);
        buffer = buffer.Slice(reader.Position);
        
        return true;
    }
    
    int ExtractDataLengthFromSetCommand(ReadOnlySequence<byte> commandLine)
    {
        var reader = new SequenceReader<byte>(commandLine);
        
        try
        {
            // Skip "set" command
            if (!reader.TryReadTo(out ReadOnlySpan<byte> _, (byte)' ')) return -1;
            
            // Skip key
            if (!reader.TryReadTo(out ReadOnlySpan<byte> _, (byte)' ')) return -1;
            
            // Skip flags
            if (!reader.TryReadTo(out ReadOnlySpan<byte> _, (byte)' ')) return -1;
            
            // Skip expiration
            if (!reader.TryReadTo(out ReadOnlySpan<byte> _, (byte)' ')) return -1;
            
            // Read data length
            if (!reader.TryReadTo(out ReadOnlySpan<byte> dataLengthSpan, (byte)' '))
            {
                // No space found, read to end (no noreply parameter)
                dataLengthSpan = reader.UnreadSpan;
            }
            
            if (int.TryParse(System.Text.Encoding.UTF8.GetString(dataLengthSpan), out int dataLength))
            {
                return dataLength;
            }
        }
        catch
        {
            // Parsing failed
        }
        
        return -1;
    }

    bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        line = default;
        var searchBuffer = buffer;
        SequencePosition? endOfLastLine = null;

        while (true)
        {
            // Look for the next newline character.
            SequencePosition? nlPosition = searchBuffer.PositionOf((byte)'\n');

            // If no more newlines are found, we're done searching.
            if (nlPosition == null)
            {
                break;
            }

            // Check for the preceding '\r' to ensure it's a valid CRLF.
            var crPosition = searchBuffer.GetPosition(-1, nlPosition.Value);
            var crSlice = searchBuffer.Slice(crPosition, 1);
            if (crSlice.First.Span[0] != (byte)'\r')
            {
                // Not a valid \r\n. Stop here, as the block of valid lines has ended.
                break;
            }

            // We found a valid line. Mark its end position and continue searching
            // from the character after the '\n'.
            endOfLastLine = searchBuffer.GetPosition(1, nlPosition.Value);
            searchBuffer = searchBuffer.Slice(endOfLastLine.Value);
        }

        // If endOfLastLine is null, it means we never found a single complete line.
        if (endOfLastLine == null)
        {
            return false;
        }

        // We found at least one line. The block is from the start of the original
        // buffer to the end of the last valid line we found.
        var totalOffset = buffer.Slice(0, endOfLastLine.Value);
        line = buffer.Slice(0, totalOffset.Length);
        buffer = buffer.Slice(totalOffset.Length);
        
        return true;
    }

    /// <summary>
    /// Writes a response back to the client asynchronously.
    /// </summary>
    /// <param name="response">The response data to send to the client.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public async Task WriteResponseAsync(byte[] response)
    {
        try
        {
            if (_stream != null && response.Length > 0 && this._client.Connected)
            {
                await _stream.WriteAsync(response);
                await _stream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing response to client {ConnectionId}", _connectionId);
        }
    }

    /// <summary>
    /// Starts the idle timer if connection idle timeout is configured.
    /// </summary>
    private void StartIdleTimer()
    {
        lock (_timerLock)
        {
            if (_settings.ConnectionIdleTimeoutSeconds > 0)
            {
                var timeoutMs = _settings.ConnectionIdleTimeoutSeconds * 1000;
                _idleTimer = new Timer(OnIdleTimeout, null, timeoutMs, Timeout.Infinite);
                _logger?.LogTrace("Started idle timer for connection {ConnectionId} with {TimeoutSeconds}s timeout", 
                    _connectionId, _settings.ConnectionIdleTimeoutSeconds);
            }
        }
    }

    /// <summary>
    /// Resets the idle timer when activity is detected.
    /// </summary>
    private void ResetIdleTimer()
    {
        lock (_timerLock)
        {
            if (_idleTimer != null && _settings.ConnectionIdleTimeoutSeconds > 0)
            {
                var timeoutMs = _settings.ConnectionIdleTimeoutSeconds * 1000;
                _idleTimer.Change(timeoutMs, Timeout.Infinite);
                _logger?.LogTrace("Reset idle timer for connection {ConnectionId}", _connectionId);
            }
        }
    }

    /// <summary>
    /// Stops the idle timer.
    /// </summary>
    private void StopIdleTimer()
    {
        lock (_timerLock)
        {
            _idleTimer?.Dispose();
            _idleTimer = null;
        }
    }

    /// <summary>
    /// Callback invoked when the idle timer expires.
    /// </summary>
    private void OnIdleTimeout(object? state)
    {
        _logger?.LogInformation("Connection {ConnectionId} idle timeout reached, closing connection", _connectionId);
        _connectionCts.Cancel();
    }

    /// <summary>
    /// Disposes of the TCP client connection and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        StopIdleTimer();
        _connectionCts.Dispose();
        _client.Dispose();
    }
}