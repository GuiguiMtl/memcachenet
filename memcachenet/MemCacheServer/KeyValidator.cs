using System.Text;

namespace memcachenet.MemCacheServer;

/// <summary>
/// Provides validation functionality for memcached keys according to protocol specifications.
/// </summary>
public static class KeyValidator
{
    /// <summary>
    /// Validates a memcached key according to protocol requirements.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <param name="maxKeySize">The maximum allowed key size in bytes.</param>
    /// <returns>A validation result indicating whether the key is valid.</returns>
    public static CommandValidationResult ValidateKey(string? key, int maxKeySize)
    {
        // Check for null or empty key
        if (string.IsNullOrEmpty(key))
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidKey, 
                "key cannot be empty");
        }
        
        // Check for whitespace-only key
        if (string.IsNullOrWhiteSpace(key))
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidKey, 
                "key cannot be whitespace only");
        }
        
        // Check key length in bytes
        var keyBytes = Encoding.UTF8.GetByteCount(key);
        if (keyBytes > maxKeySize)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidKey, 
                $"key too long (max {maxKeySize} bytes)");
        }
        
        // Check for invalid characters according to memcached protocol
        foreach (char c in key)
        {
            // Control characters (0-31) and DEL (127) are not allowed
            if (c <= 31 || c == 127)
            {
                return CommandValidationResult.Failure(
                    ValidationErrorType.InvalidKey, 
                    "key contains control characters");
            }
            
            // Space character is not allowed in keys
            if (c == ' ')
            {
                return CommandValidationResult.Failure(
                    ValidationErrorType.InvalidKey, 
                    "key contains spaces");
            }
        }
        
        // Validate UTF-8 encoding
        try
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            var decoded = Encoding.UTF8.GetString(bytes);
            if (decoded != key)
            {
                return CommandValidationResult.Failure(
                    ValidationErrorType.InvalidKey, 
                    "key contains invalid UTF-8 sequences");
            }
        }
        catch (Exception)
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.InvalidKey, 
                "key contains invalid characters");
        }
        
        return CommandValidationResult.Success();
    }
    
    /// <summary>
    /// Validates multiple keys for a multi-key operation like GET.
    /// </summary>
    /// <param name="keys">The keys to validate.</param>
    /// <param name="maxKeySize">The maximum allowed key size in bytes.</param>
    /// <returns>A validation result indicating whether all keys are valid.</returns>
    public static CommandValidationResult ValidateKeys(IEnumerable<string> keys, int maxKeySize)
    {
        if (!keys.Any())
        {
            return CommandValidationResult.Failure(
                ValidationErrorType.MissingParameter, 
                "no keys provided");
        }
        
        foreach (var key in keys)
        {
            var result = ValidateKey(key, maxKeySize);
            if (!result.IsValid)
            {
                return result; // Return first validation failure
            }
        }
        
        return CommandValidationResult.Success();
    }
}