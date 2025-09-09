namespace memcachenet.MemCacheServer.Validations;

/// <summary>
/// Represents the types of validation errors that can occur during command parsing.
/// </summary>
public enum ValidationErrorType
{
    /// <summary>
    /// Unknown or unrecognized command.
    /// </summary>
    UnknownCommand,
    
    /// <summary>
    /// Invalid key format, length, or characters.
    /// </summary>
    InvalidKey,
    
    /// <summary>
    /// Invalid command parameter value or format.
    /// </summary>
    InvalidParameter,
    
    /// <summary>
    /// Missing required parameters.
    /// </summary>
    MissingParameter,
    
    /// <summary>
    /// Invalid data block or data length mismatch.
    /// </summary>
    InvalidData,
    
    /// <summary>
    /// Protocol violation (malformed command structure, invalid line endings, etc.).
    /// </summary>
    ProtocolViolation,
    
    /// <summary>
    /// Server-side error (limits exceeded, etc.).
    /// </summary>
    ServerError
}

/// <summary>
/// Represents the result of command validation with detailed error information.
/// </summary>
public class CommandValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid { get; private set; }
    
    /// <summary>
    /// Gets the type of validation error if validation failed.
    /// </summary>
    public ValidationErrorType? ErrorType { get; private set; }
    
    /// <summary>
    /// Gets the detailed error message describing the validation failure.
    /// </summary>
    public string ErrorMessage { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets the appropriate memcached protocol response for this validation result.
    /// </summary>
    public string ProtocolResponse => FormatProtocolResponse();
    
    private CommandValidationResult(bool isValid, ValidationErrorType? errorType, string errorMessage)
    {
        IsValid = isValid;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }
    
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A validation result indicating success.</returns>
    public static CommandValidationResult Success() => new(true, null, string.Empty);
    
    /// <summary>
    /// Creates a failed validation result with the specified error information.
    /// </summary>
    /// <param name="errorType">The type of validation error.</param>
    /// <param name="errorMessage">The detailed error message.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static CommandValidationResult Failure(ValidationErrorType errorType, string errorMessage)
        => new(false, errorType, errorMessage);
    
    /// <summary>
    /// Formats the appropriate memcached protocol response based on the error type.
    /// </summary>
    /// <returns>The formatted protocol response string.</returns>
    private string FormatProtocolResponse()
    {
        if (IsValid)
            return string.Empty;
            
        return ErrorType switch
        {
            ValidationErrorType.UnknownCommand => "ERROR",
            ValidationErrorType.InvalidKey => $"CLIENT_ERROR {ErrorMessage}",
            ValidationErrorType.InvalidParameter => $"CLIENT_ERROR {ErrorMessage}",
            ValidationErrorType.MissingParameter => $"CLIENT_ERROR {ErrorMessage}",
            ValidationErrorType.InvalidData => $"CLIENT_ERROR {ErrorMessage}",
            ValidationErrorType.ProtocolViolation => $"CLIENT_ERROR {ErrorMessage}",
            ValidationErrorType.ServerError => $"SERVER_ERROR {ErrorMessage}",
            _ => $"ERROR {ErrorMessage}"
        };
    }
}