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

            while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
            {
                // invoke the callback when a line is read
                onLineRead?.Invoke(line, WriteResponseAsync);
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
    bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        // Look for \r\n in the buffer (memcache protocol requires CRLF).
        SequencePosition? nlPosition = buffer.PositionOf((byte)'\n');

        if (nlPosition == null)
        {
            line = default;
            return false;
        }

        // Check if there's a \r immediately before the \n
        // Check if we have enough data before the \n position for \r\n
        var beforeN = buffer.Slice(0, nlPosition.Value);
        if (beforeN.Length == 0)
        {
            // \n is at the very beginning, no \r possible
            line = default;
            return false;
        }
        
        // Check the last byte before \n to see if it's \r
        var lastByte = beforeN.Slice(beforeN.Length - 1, 1).First.Span[0];
        if (lastByte != (byte)'\r')
        {
            // No \r before \n, this is not a valid CRLF line ending
            line = default;
            return false;
        }

        // Include the line + the \r\n in the extracted line.
        line = buffer.Slice(0, buffer.GetPosition(1, nlPosition.Value));
        buffer = buffer.Slice(buffer.GetPosition(1, nlPosition.Value));
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