using System.Buffers;
using System.Globalization;

namespace memcachenet.MemCacheServer.Validations;

/// <summary>
/// Provides validation functionality for memcached commands and their parameters.
/// </summary>
public static class CommandValidator
{
    /// <summary>
    /// Validates that a command line ends with proper CRLF termination.
    /// </summary>
    /// <param name="buffer">The command buffer to validate.</param>
    /// <returns>A validation result indicating whether the command has proper termination.</returns>
    public static CommandValidationResult ValidateCommandTermination(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 2)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.ProtocolViolation,
                "command too short");
        }
        
        // Check if command ends with \r\n
        var lastTwoBytes = buffer.Slice(buffer.Length - 2, 2).ToArray();
        if (lastTwoBytes[0] != (byte)'\r' || lastTwoBytes[1] != (byte)'\n')
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.ProtocolViolation,
                "command must end with \\r\\n");
        }
        
        return CommandValidationResult.Success();
    }
    
    /// <summary>
    /// Validates that a numeric parameter is within acceptable bounds.
    /// </summary>
    /// <param name="value">The parsed numeric value.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="minValue">The minimum acceptable value (inclusive).</param>
    /// <param name="maxValue">The maximum acceptable value (inclusive).</param>
    /// <returns>A validation result indicating whether the parameter is valid.</returns>
    public static CommandValidationResult ValidateNumericParameter<T>(T value, string parameterName, T minValue, T maxValue) 
        where T : IComparable<T>
    {
        if (value.CompareTo(minValue) < 0)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidParameter,
                $"{parameterName} cannot be less than {minValue}");
        }
        
        if (value.CompareTo(maxValue) > 0)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidParameter,
                $"{parameterName} cannot be greater than {maxValue}");
        }
        
        return CommandValidationResult.Success();
    }
    
    /// <summary>
    /// Validates the data length parameter for SET commands.
    /// </summary>
    /// <param name="dataLength">The declared data length.</param>
    /// <param name="actualDataLength">The actual data length received.</param>
    /// <param name="maxDataSize">The maximum allowed data size.</param>
    /// <returns>A validation result indicating whether the data length is valid.</returns>
    public static CommandValidationResult ValidateDataLength(int dataLength, int actualDataLength, int maxDataSize)
    {
        // Check for negative data length
        if (dataLength < 0)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidParameter,
                "data length cannot be negative");
        }
        
        // Check against maximum data size
        if (dataLength > maxDataSize)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.ServerError,
                $"data length exceeds maximum allowed size ({maxDataSize} bytes)");
        }
        
        // Check data length mismatch
        if (dataLength != actualDataLength)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidData,
                $"data length mismatch: expected {dataLength} bytes, got {actualDataLength} bytes");
        }
        
        return CommandValidationResult.Success();
    }
    
    /// <summary>
    /// Validates that the required number of parameters are present.
    /// </summary>
    /// <param name="parameterCount">The actual number of parameters found.</param>
    /// <param name="expectedCount">The expected number of parameters.</param>
    /// <param name="commandName">The name of the command being validated.</param>
    /// <returns>A validation result indicating whether the parameter count is correct.</returns>
    public static CommandValidationResult ValidateParameterCount(int parameterCount, int expectedCount, string commandName)
    {
        if (parameterCount < expectedCount)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.MissingParameter,
                $"{commandName} command requires {expectedCount} parameters");
        }
        
        return CommandValidationResult.Success();
    }
    
    /// <summary>
    /// Tries to parse a UTF-8 numeric parameter from a byte span.
    /// </summary>
    /// <typeparam name="T">The numeric type to parse.</typeparam>
    /// <param name="span">The byte span containing the numeric value.</param>
    /// <param name="parameterName">The name of the parameter being parsed.</param>
    /// <param name="value">The parsed value if successful.</param>
    /// <returns>A validation result indicating whether parsing was successful.</returns>
    public static CommandValidationResult TryParseNumericParameter<T>(ReadOnlySpan<byte> span, string parameterName, out T value) 
        where T : IUtf8SpanParsable<T>
    {
        if (!T.TryParse(span, new NumberFormatInfo(), out value))
        {
            value = default!;
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidParameter,
                $"invalid {parameterName} format");
        }
        
        return CommandValidationResult.Success();
    }
    
    /// <summary>
    /// Validates that a data block follows the correct protocol format (ends with \r\n).
    /// </summary>
    /// <param name="dataBlock">The data block to validate.</param>
    /// <returns>A validation result indicating whether the data block format is correct.</returns>
    public static CommandValidationResult ValidateDataBlockFormat(ReadOnlySpan<byte> dataBlock)
    {
        // Data blocks should be followed by \r\n, but this is checked at the protocol level
        // This method is for future extensions if needed
        return CommandValidationResult.Success();
    }
}