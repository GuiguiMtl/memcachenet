using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace memcachenet.MemCacheServer;

public class MemCacheConnectionHandler : IDisposable
{
    private readonly TcpClient _client;
    private readonly Action<ReadOnlySequence<byte>>? onLineRead;

    public MemCacheConnectionHandler(TcpClient client,
    Action<ReadOnlySequence<byte>>? onLineRead)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }
        
        _client = client;
        this.onLineRead = onLineRead;
    }

    public async Task HandleConnectionAsync()
    {
        var stream = _client.GetStream();
        var pipe = new Pipe();
        Task writing = FillPipeAsync(stream, pipe.Writer);
        Task reading = ReadPipeAsync(pipe.Reader);
        
        await Task.WhenAll(reading, writing);
    }

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

    private async Task ReadPipeAsync(PipeReader reader)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
            {
                // invoke the callback when a line is read
                onLineRead?.Invoke(line);
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

    public void Dispose()
    {
        _client.Dispose();
    }
}