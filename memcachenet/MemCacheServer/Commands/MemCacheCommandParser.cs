using System.Buffers;
using System.Globalization;
using System.Text;
using memcachenet.MemCacheServer.Validations;

namespace memcachenet.MemCacheServer.Commands;

/// <summary>
/// Parses raw byte sequences into structured MemCache commands (GET, SET, DELETE).
/// </summary>
/// <param name="maxKeySize">The maximum allowed size for cache keys in bytes.</param>
/// <param name="maxDataSize">The maximum allowed size for cache data in bytes.</param>
public class MemCacheCommandParser(int maxKeySize, int maxDataSize) : ICommandParser<IMemCacheCommand>
{
    private const string InvalidCommand = "invalid command";
    
    /// <summary>
    /// Parses a byte buffer into a MemCache command.
    /// </summary>
    /// <param name="buffer">The byte sequence containing the command to parse.</param>
    /// <returns>A parsed MemCache command or an InvalidMemCacheCommand if parsing fails.</returns>
    public IMemCacheCommand ParseCommand(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        // 1. Read the first word (the command) up to the first space.
        if (!reader.TryReadTo(out ReadOnlySpan<byte> commandSpan, (byte)' '))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.ProtocolViolation, "invalid command format");
            return new InvalidMemCacheCommand(result);
        }

        var command = Encoding.UTF8.GetString(commandSpan);
        switch (command.ToLowerInvariant())
        {
            case "get":
                return HandleGetCommand(ref reader);
            case "set":
                return HandleSetCommand(ref reader);
            case "delete":
                return HandleDeleteCommand(reader);
            default:
                var unknownResult = CommandValidationResult.Failure(ValidationErrorType.UnknownCommand, $"unknown command: {command}");
                return new InvalidMemCacheCommand(unknownResult);
        }
    }

    /// <summary>
    /// Handles parsing of DELETE commands to remove keys from the cache.
    /// </summary>
    /// <param name="reader">The sequence reader positioned after the "delete" command word.</param>
    /// <returns>A DeleteMemCacheCommand or InvalidMemCacheCommand if parsing fails.</returns>
    private IMemCacheCommand HandleDeleteCommand(SequenceReader<byte> reader)
    {
        // Read the parameters line until \r\n
        if (!reader.TryReadTo(out ReadOnlySequence<byte> parametersLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.ProtocolViolation, "missing command termination");
            return new InvalidMemCacheCommand(result);
        }

        var parametersReader = new SequenceReader<byte>(parametersLine);
        
        // If we can read up to a space we might have an extra 'noreply' parameter
        if (parametersReader.TryReadTo(out ReadOnlySpan<byte> keySpan, (byte)' '))
        {
            var key = Encoding.UTF8.GetString(keySpan);
            var keyValidation = KeyValidator.ValidateKey(key, maxKeySize);
            if (!keyValidation.IsValid)
            {
                return new InvalidMemCacheCommand(keyValidation);
            }
            
            var noReplyString = Encoding.UTF8.GetString(parametersReader.UnreadSequence);
            if (!noReplyString.Equals("noreply", StringComparison.Ordinal))
            {
                var result = CommandValidationResult.Failure(ValidationErrorType.InvalidParameter, $"unknown parameter: {noReplyString}");
                return new InvalidMemCacheCommand(result);
            }
            
            return new DeleteMemCacheCommand
            {
                Key = key,
                NoReply = true
            };
        }

        // No space found, entire remaining sequence should be the key
        if (parametersReader.Remaining == 0)
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.MissingParameter, "missing key parameter");
            return new InvalidMemCacheCommand(result);
        }
        
        var keyOnly = Encoding.UTF8.GetString(parametersReader.UnreadSequence);
        var keyOnlyValidation = KeyValidator.ValidateKey(keyOnly, maxKeySize);
        if (!keyOnlyValidation.IsValid)
        {
            return new InvalidMemCacheCommand(keyOnlyValidation);
        }

        return new DeleteMemCacheCommand
        {
            Key = keyOnly,
            NoReply = false
        };
    }

    /// <summary>
    /// Handles parsing of SET commands to store key-value pairs in the cache.
    /// </summary>
    /// <param name="reader">The sequence reader positioned after the "set" command word.</param>
    /// <returns>A SetMemCacheCommand or InvalidMemCacheCommand if parsing fails.</returns>
    private IMemCacheCommand HandleSetCommand(ref SequenceReader<byte> reader)
    {
        // Read the parameters line until \r\n
        if (!reader.TryReadTo(out ReadOnlySequence<byte> parametersLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.ProtocolViolation, "missing command termination");
            return new InvalidMemCacheCommand(result);
        }

        var parametersReader = new SequenceReader<byte>(parametersLine);
        
        // Parse and validate key
        if (!parametersReader.TryReadTo(out ReadOnlySpan<byte> keySpan, (byte)' '))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.MissingParameter, "missing key parameter");
            return new InvalidMemCacheCommand(result);
        }
        
        var key = Encoding.UTF8.GetString(keySpan);
        var keyValidation = KeyValidator.ValidateKey(key, maxKeySize);
        if (!keyValidation.IsValid)
        {
            return new InvalidMemCacheCommand(keyValidation);
        }
        
        // Parse and validate flags
        if (!parametersReader.TryReadTo(out ReadOnlySpan<byte> flagsSpan, (byte)' '))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.MissingParameter, "missing flags parameter");
            return new InvalidMemCacheCommand(result);
        }
        
        var flagsValidation = CommandValidator.TryParseNumericParameter<uint>(flagsSpan, "flags", out uint flags);
        if (!flagsValidation.IsValid)
        {
            return new InvalidMemCacheCommand(flagsValidation);
        }
        
        // Parse and validate expiration
        if (!parametersReader.TryReadTo(out ReadOnlySpan<byte> expirationSpan, (byte)' '))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.MissingParameter, "missing expiration parameter");
            return new InvalidMemCacheCommand(result);
        }
        
        var expirationValidation = CommandValidator.TryParseNumericParameter<uint>(expirationSpan, "expiration", out uint expiration);
        if (!expirationValidation.IsValid)
        {
            return new InvalidMemCacheCommand(expirationValidation);
        }
        
        // Parse data length - this might be the last parameter or followed by noreply
        ReadOnlySpan<byte> dataLengthSpan;
        bool hasMoreParameters = false;
        
        if (parametersReader.TryReadTo(out dataLengthSpan, (byte)' '))
        {
            hasMoreParameters = true;
        }
        else
        {
            // No space found, read to end
            dataLengthSpan = parametersReader.UnreadSpan;
        }
        
        var dataLengthValidation = CommandValidator.TryParseNumericParameter<int>(dataLengthSpan, "data length", out int dataLength);
        if (!dataLengthValidation.IsValid)
        {
            return new InvalidMemCacheCommand(dataLengthValidation);
        }
        
        // Validate data length bounds
        var dataLengthBoundsValidation = CommandValidator.ValidateNumericParameter(dataLength, "data length", 0, maxDataSize);
        if (!dataLengthBoundsValidation.IsValid)
        {
            return new InvalidMemCacheCommand(dataLengthBoundsValidation);
        }
        
        // Check for noreply parameter
        bool noReply = false;
        if (hasMoreParameters)
        {
            if (parametersReader.TryReadTo(out ReadOnlySpan<byte> noReplySpan, (byte)' '))
            {
                var noReplyString = Encoding.UTF8.GetString(noReplySpan);
                if (noReplyString.Equals("noreply", StringComparison.Ordinal))
                {
                    noReply = true;
                    
                    // Check if there are more parameters after noreply (which is invalid)
                    if (parametersReader.Remaining > 0)
                    {
                        var result = CommandValidationResult.Failure(ValidationErrorType.InvalidParameter, "unexpected parameters after noreply");
                        return new InvalidMemCacheCommand(result);
                    }
                }
                else
                {
                    var result = CommandValidationResult.Failure(ValidationErrorType.InvalidParameter, $"unknown parameter: {noReplyString}");
                    return new InvalidMemCacheCommand(result);
                }
            }
            else
            {
                // Read the remaining as potential noreply parameter
                var noReplyString = Encoding.UTF8.GetString(parametersReader.UnreadSequence);
                if (noReplyString.Equals("noreply", StringComparison.Ordinal))
                {
                    noReply = true;
                }
                else
                {
                    var result = CommandValidationResult.Failure(ValidationErrorType.InvalidParameter, $"unknown parameter: {noReplyString}");
                    return new InvalidMemCacheCommand(result);
                }
            }
        }

        // Read the data block - it should be exactly dataLength bytes
        if (!TryReadDataWithValidation(reader, dataLength, out byte[] data, out var dataValidation))
        {
            return new InvalidMemCacheCommand(dataValidation);
        }
        
        return new SetMemCacheCommand
        {
            Key = key,
            Flags = flags,
            Expiration = expiration,
            Data = data,
            NoReply = noReply
        };
    }
    
    /// <summary>
    /// Handles parsing of GET commands to retrieve values from the cache.
    /// </summary>
    /// <param name="reader">The sequence reader positioned after the "get" command word.</param>
    /// <returns>A GetMemCacheCommand or InvalidMemCacheCommand if parsing fails.</returns>
    private IMemCacheCommand HandleGetCommand(ref SequenceReader<byte> reader)
    {
        // Read the rest of the command until \r\n
        if (!reader.TryReadTo(out ReadOnlySequence<byte> keysLine, new byte[] { (byte)'\r', (byte)'\n' }))
        {
            var result = CommandValidationResult.Failure(ValidationErrorType.ProtocolViolation, "missing command termination");
            return new InvalidMemCacheCommand(result);
        }

        var keysReader = new SequenceReader<byte>(keysLine);
        var keys = new List<string>();
        
        // Loop through the bytes to extract each space-separated key
        while (keysReader.Remaining > 0)
        {
            if (keysReader.TryReadTo(out ReadOnlySpan<byte> keySpan, (byte)' '))
            {
                if (!keySpan.IsEmpty)
                {
                    var key = Encoding.UTF8.GetString(keySpan);
                    var keyValidation = KeyValidator.ValidateKey(key, maxKeySize);
                    if (!keyValidation.IsValid)
                    {
                        return new InvalidMemCacheCommand(keyValidation);
                    }
                    keys.Add(key);
                }
            }
            else
            {
                // No more spaces, the rest of the sequence is the last key
                if (keysReader.Remaining > 0)
                {
                    var lastKey = Encoding.UTF8.GetString(keysReader.UnreadSequence);
                    var keyValidation = KeyValidator.ValidateKey(lastKey, maxKeySize);
                    if (!keyValidation.IsValid)
                    {
                        return new InvalidMemCacheCommand(keyValidation);
                    }
                    keys.Add(lastKey);
                }
                break;
            }
        }

        // Validate that at least one key was provided
        var keysValidation = KeyValidator.ValidateKeys(keys, maxKeySize);
        if (!keysValidation.IsValid)
        {
            return new InvalidMemCacheCommand(keysValidation);
        }

        return new GetMemCacheCommand
        {
            Keys = keys.ToArray()
        };
    }

    /// <summary>
    /// Attempts to read a specific amount of data bytes from the sequence reader.
    /// </summary>
    /// <param name="reader">The sequence reader to read from.</param>
    /// <param name="dataLength">The number of bytes to read.</param>
    /// <param name="data">The output byte array containing the read data.</param>
    /// <returns>True if the data was successfully read; otherwise, false.</returns>
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

    /// <summary>
    /// Attempts to read a specific amount of data bytes from the sequence reader with validation.
    /// </summary>
    /// <param name="reader">The sequence reader to read from.</param>
    /// <param name="dataLength">The number of bytes to read.</param>
    /// <param name="data">The output byte array containing the read data.</param>
    /// <param name="validationResult">The validation result if reading fails.</param>
    /// <returns>True if the data was successfully read; otherwise, false.</returns>
    private bool TryReadDataWithValidation(SequenceReader<byte> reader, int dataLength, out byte[] data, out CommandValidationResult validationResult)
    {
        data = [];
        validationResult = CommandValidationResult.Success();
        
        if (reader.Remaining < dataLength)
        {
            validationResult = CommandValidationResult.Failure(ValidationErrorType.InvalidData, "insufficient data available");
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
            else
            {
                // Check if we have at least \r\n somewhere in the remaining data
                // This handles cases where there might be more data after this command
                validationResult = CommandValidationResult.Failure(ValidationErrorType.ProtocolViolation, "data block must end with \\r\\n");
                return false;
            }
        }
        else
        {
            validationResult = CommandValidationResult.Failure(ValidationErrorType.InvalidData, "missing data termination");
            return false;
        }
        
        // Final validation of actual data length
        var actualDataLength = data.Length;
        var dataLengthValidation = CommandValidator.ValidateDataLength(dataLength, actualDataLength, maxDataSize);
        if (!dataLengthValidation.IsValid)
        {
            validationResult = dataLengthValidation;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to read a string from the sequence reader up to the next space character.
    /// </summary>
    /// <param name="reader">The sequence reader to read from.</param>
    /// <param name="key">The output string value.</param>
    /// <returns>True if the string was successfully read and is within size limits; otherwise, false.</returns>
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
    
    /// <summary>
    /// Attempts to read a numeric value from the sequence reader up to the next space character.
    /// </summary>
    /// <typeparam name="T">The numeric type to parse, must implement IUtf8SpanParsable.</typeparam>
    /// <param name="reader">The sequence reader to read from.</param>
    /// <param name="key">The output parsed numeric value.</param>
    /// <returns>True if the numeric value was successfully parsed; otherwise, false.</returns>
    private bool TryGetNumeric<T>(ref SequenceReader<byte> reader, out T? key) where T : IUtf8SpanParsable<T> 
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

}