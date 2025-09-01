using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Handles TCP client connections for the MemCache server, managing data reading and writing using pipelines.
/// </summary>
/// <param name="client">The TCP client connection to handle.</param>
/// <param name="onLineRead">Callback invoked when a complete line is read from the client, with a response writer function.</param>
public class MemCacheConnectionHandler(
    TcpClient client,
    Action<ReadOnlySequence<byte>, Func<byte[], Task>>? onLineRead)
    : IDisposable
{
    /// <summary>
    /// The TCP client connection being handled.
    /// </summary>
    private readonly TcpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    
    /// <summary>
    /// The network stream for reading from and writing to the client.
    /// </summary>
    private NetworkStream? _stream;

    /// <summary>
    /// Handles the client connection asynchronously by setting up reading and writing pipelines.
    /// </summary>
    /// <returns>A task representing the asynchronous connection handling operation.</returns>
    public async Task HandleConnectionAsync()
    {
        _stream = _client.GetStream();
        var pipe = new Pipe();
        Task writing = FillPipeAsync(_stream, pipe.Writer);
        Task reading = ReadPipeAsync(pipe.Reader);
        
        await Task.WhenAll(reading, writing);
    }

    /// <summary>
    /// Continuously reads data from the network stream and writes it to the pipe writer.
    /// </summary>
    /// <param name="stream">The network stream to read from.</param>
    /// <param name="writer">The pipe writer to write data to.</param>
    /// <returns>A task representing the asynchronous fill operation.</returns>
    private async Task FillPipeAsync(NetworkStream stream, PipeWriter writer)
    {
        const int minimumBufferSize = 512;

        while (true)
        {
            // Allocate at least 512 bytes from the PipeWriter.
            Memory<byte> memory = writer.GetMemory(minimumBufferSize);
            try
            {
                int bytesRead = await stream.ReadAsync(memory);
                if (bytesRead == 0)
                {
                    break;
                }
                // Tell the PipeWriter how much was read from the Socket.
                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                // TODO log
                break;
            }

            // Make the data available to the PipeReader.
            FlushResult result = await writer.FlushAsync();

            if (result.IsCompleted)
            {
                break;
            }
        }

        // By completing PipeWriter, tell the PipeReader that there's no more data coming.
        await writer.CompleteAsync();
    }

    /// <summary>
    /// Continuously reads data from the pipe reader and processes complete lines.
    /// </summary>
    /// <param name="reader">The pipe reader to read from.</param>
    /// <returns>A task representing the asynchronous read operation.</returns>
    private async Task ReadPipeAsync(PipeReader reader)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadMemCacheCommand(ref buffer, out ReadOnlySequence<byte> command))
            {
                // invoke the callback when a command is read
                onLineRead?.Invoke(command, WriteResponseAsync);
            }

            // Tell the PipeReader how much of the buffer has been consumed.
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Stop reading if there's no more data coming.
            if (result.IsCompleted)
            {
                break;
            }
        }

        // Mark the PipeReader as complete.
        await reader.CompleteAsync();
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
            return false; // Invalid SET command format
        }
        
        // Check if we have enough data for the complete command (data + \r\n)
        if (reader.Remaining < dataLength + 2)
        {
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
        if (_stream != null && response.Length > 0)
        {
            await _stream.WriteAsync(response);
            await _stream.FlushAsync();
        }
    }

    /// <summary>
    /// Disposes of the TCP client connection and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }
}