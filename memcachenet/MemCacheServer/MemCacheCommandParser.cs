using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Unicode;

namespace memcachenet.MemCacheServer;

public class MemCacheCommandParser(int maxKeySize, int maxDataSize) : ICommandParser<IMemCacheCommand>
{
    private const string INVALID_COMMAND = "invalid command";
    public IMemCacheCommand ParseCommand(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        // 1. Read the first word (the command) up to the first space.
        if (!reader.TryReadTo(out ReadOnlySpan<byte> commandSpan, (byte)' '))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        var command = Encoding.UTF8.GetString(commandSpan);
        switch (command)
        {
            case "get":
                return HandleGetCommand(ref reader);
            case "set":
                return HandleSetCommand(ref reader);
            case "delete":
                return HandleDeleteCommand(reader);
            default:
                return new InvalidMemCacheCommand(INVALID_COMMAND);
        }
    }

    private IMemCacheCommand HandleDeleteCommand(SequenceReader<byte> reader)
    {
        // Read the parameters line until \r\n
        if (!reader.TryReadTo(out ReadOnlySequence<byte> parametersLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        var parametersReader = new SequenceReader<byte>(parametersLine);
        
        // If we can read up to a space we might have an extra 'noreply' parameter
        if (parametersReader.TryReadTo(out ReadOnlySpan<byte> keySpan, (byte)' '))
        {
            // Check if the key to delete is not too long
            if (keySpan.Length > maxKeySize)
            {
                return new InvalidMemCacheCommand(INVALID_COMMAND);
            }
            
            var key = Encoding.UTF8.GetString(keySpan);
            
            var noReplyString = Encoding.UTF8.GetString(parametersReader.UnreadSequence);
            if (!noReplyString.Equals("noreply"))
            {
                // Invalid last parameter
                return new InvalidMemCacheCommand(INVALID_COMMAND);
            }
            
            return new DeleteMemCacheCommand
            {
                Key = key,
                NoReply = true
            };
        }

        // Check if the key to delete is not too long
        if (parametersReader.Remaining > maxKeySize)
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }
        
        var keyOnly = Encoding.UTF8.GetString(parametersReader.UnreadSequence);

        return new DeleteMemCacheCommand
        {
            Key = keyOnly,
            NoReply = false
        };
    }

    private IMemCacheCommand HandleSetCommand(ref SequenceReader<byte> reader)
    {
        byte[] data;
        // Read the parameters line until \r\n
        if (!reader.TryReadTo(out ReadOnlySequence<byte> parametersLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        var parametersReader = new SequenceReader<byte>(parametersLine);
        
        // Get the key name
        if (!TryGetString(ref parametersReader, out var key))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }            
        
        // Validate the key is not empty
        if(String.IsNullOrWhiteSpace(key))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        } 
        
        // Get the flag
        if (!TryGetNumeric(ref parametersReader, out uint flag))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }
        
        // Get the expiration time
        if (!TryGetNumeric(ref parametersReader, out uint expirationTime))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        // Get the data length (bytes)
        if (!TryGetNumeric(ref parametersReader, out int dataLength))
        {
            Int32.TryParse(parametersReader.UnreadSpan, null, out dataLength);

            if (dataLength > maxDataSize)
            {
                return new InvalidMemCacheCommand(INVALID_COMMAND);
            }
            
            if (!TryReadData(reader, dataLength, out data))
            {
                return new InvalidMemCacheCommand(INVALID_COMMAND);
            }
            
            return new SetMemCacheCommand
            {
                Key = key,
                Flags = flag,
                Expiration = expirationTime,
                Data = data,
                NoReply = false
            };
        }
        
        // Validate data length
        if (dataLength < 0 || dataLength > maxDataSize)
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        // Check for optional noreply parameter
        bool noReply = false;
        if (parametersReader.Remaining > 0)
        {
            if (parametersReader.TryReadTo(out ReadOnlySpan<byte> noReplySpan, (byte)' '))
            {
                var noReplyString = Encoding.UTF8.GetString(noReplySpan);
                if (noReplyString.Equals("noreply"))
                {
                    noReply = true;
                }
                else
                {
                    return new InvalidMemCacheCommand(INVALID_COMMAND);
                }
                
                // Check if there are more parameters after noreply (which is invalid)
                if (parametersReader.Remaining > 0)
                {
                    return new InvalidMemCacheCommand(INVALID_COMMAND);
                }
            }
            else
            {
                // Read the remaining as noreply parameter
                var noReplyString = Encoding.UTF8.GetString(parametersReader.UnreadSequence);
                if (noReplyString.Equals("noreply"))
                {
                    noReply = true;
                }
                else
                {
                    return new InvalidMemCacheCommand(INVALID_COMMAND);
                }
            }
        }

        // Read the data block - it should be exactly dataLength bytes
        if (!TryReadData(reader, dataLength, out data))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }
        
        return new SetMemCacheCommand
        {
            Key = key,
            Flags = flag,
            Expiration = expirationTime,
            Data = data,
            NoReply = noReply
        };
    }

    private bool TryReadData(SequenceReader<byte> reader, int dataLength, out byte[] data)
    {
        data = [];
        if (reader.Remaining < dataLength)
        {
            return false;
        }

        data = reader.UnreadSequence.Slice(0, dataLength).ToArray();
        reader.Advance(dataLength);
        
        // The data block should be followed by \r\n according to protocol
        if (reader.Remaining >= 2)
        {
            var nextBytes = reader.UnreadSequence.Slice(0, 2).ToArray();
            if (nextBytes[0] == (byte)'\r' && nextBytes[1] == (byte)'\n')
            {
                reader.Advance(2);
            }
            // Note: We don't fail if \r\n is missing as the protocol allows any bytes in data
        }

        return true;
    }

    private bool TryGetString(ref SequenceReader<byte> reader, out string key)
    {
        key = String.Empty;
        if (!reader.TryReadTo(out ReadOnlySpan<byte> keySpan, (byte)' '))
        {
            // Command is invalid
            return false;
        }

        if (keySpan.Length > maxKeySize)
        {
            // Key is too long
            return false;
        }
        key = Encoding.UTF8.GetString(keySpan);
        return true;
    }
    
    private bool TryGetNumeric<T>(ref SequenceReader<byte> reader, out T key) where T : IUtf8SpanParsable<T> 
    {
        key = default;
        if (!reader.TryReadTo(out ReadOnlySpan<byte> span, (byte)' '))
        {
            // Command is invalid
            return false;
        }

        if (!T.TryParse(span, new NumberFormatInfo(), out key))
        {
            return false;
        }

        return true;
    }

    private IMemCacheCommand HandleGetCommand(ref SequenceReader<byte> reader)
    {
        // Read the rest of the command until \r\n
        if (!reader.TryReadTo(out ReadOnlySequence<byte> keysLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        var keysReader = new SequenceReader<byte>(keysLine);
        var keys = new List<string>();
        
        // Loop through the bytes to extract each space-separated key
        while (keysReader.Remaining > 0)
        {
            if (keysReader.TryReadTo(out ReadOnlySpan<byte> keySpan, (byte)' '))
            {
                if (keySpan.Length > maxKeySize)
                {
                    return new InvalidMemCacheCommand(INVALID_COMMAND);
                }
                
                if (!keySpan.IsEmpty)
                {
                    keys.Add(Encoding.UTF8.GetString(keySpan));
                }
            }
            else
            {
                // No more spaces, the rest of the sequence is the last key
                if (keysReader.Remaining > 0)
                {
                    var lastKey = keysReader.UnreadSequence;
                    if (lastKey.Length > maxKeySize)
                    {
                        return new InvalidMemCacheCommand(INVALID_COMMAND);
                    }
                    keys.Add(Encoding.UTF8.GetString(lastKey));
                }
                break;
            }
        }

        if (keys.Count == 0)
        {
            return new InvalidMemCacheCommand(INVALID_COMMAND);
        }

        return new GetMemCacheCommand
        {
            Keys = keys.ToArray()
        };
    }
}