using System.Net.Sockets;
using System.Text;
using FluentAssertions;

namespace MemCacheNet.IntegrationTests;

/// <summary>
/// Integration tests for server limits and constraints as specified in assignment requirements.
/// Tests key size limits, value size limits, max keys, and total size constraints.
/// </summary>
public class ServerLimitsTests
{
    public int TestPort = 11211;

    public ServerLimitsTests()
    {
    }
    
    // protected override int GetMaxKeys() => 10; // Small number for easier testing
    // protected override int GetMaxDataSize() => 1024; // 1KB for easier testing (assignment: 100KB)
    // protected override int GetMaxKeySize() => 50; // Smaller for testing (assignment: 250 bytes)

    [Fact]
    public async Task SetWithOversizedKey_ShouldRejectCommand()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Create a key that exceeds the limit (MaxKeySizeBytes = 50)
        var oversizedKey = new string('a', 51);
        var value = "test_value";

        // Act
        var setCommand = $"set {oversizedKey} 0 0 {value.Length}\r\n{value}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Server should reject oversized keys");
    }

    [Fact]
    public async Task SetWithOversizedValue_ShouldRejectCommand()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "test_key";
        // Create a value that exceeds the limit (MaxDataSizeBytes = 1024)
        var oversizedValue = new string('x', 1025);

        // Act
        var setCommand = $"set {key} 0 0 {oversizedValue.Length}\r\n{oversizedValue}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var response = new byte[2048]; // Larger buffer for potential error response
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Server should reject oversized values");
    }

    [Fact]
    public async Task SetAtMaxKeySize_ShouldSucceed()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Create a key at exactly the maximum size (MaxKeySizeBytes = 50)
        var maxSizeKey = new string('b', 50);
        var value = "test_value";

        // Act
        var setCommand = $"set {maxSizeKey} 0 0 {value.Length}\r\n{value}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("STORED", "Keys at maximum size should be accepted");

        // Verify we can retrieve it
        var getCommand = $"get {maxSizeKey}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
        var getResponse = new byte[1024];
        var getBytesRead = await stream.ReadAsync(getResponse);
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);
        getResponseText.Should().Contain($"VALUE {maxSizeKey}");
        getResponseText.Should().Contain(value);
    }

    [Fact]
    public async Task SetAtMaxValueSize_ShouldSucceed()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "max_value_test";
        // Create a value at exactly the maximum size (MaxDataSizeBytes = 1024)
        var maxSizeValue = new string('y', 1024);

        // Act
        var setCommand = $"set {key} 0 0 {maxSizeValue.Length}\r\n{maxSizeValue}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("STORED", "Values at maximum size should be accepted");

        // Verify we can retrieve it
        var getCommand = $"get {key}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
        var getResponse = new byte[2048]; // Larger buffer for max size value
        var getBytesRead = await stream.ReadAsync(getResponse);
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);
        getResponseText.Should().Contain($"VALUE {key}");
        getResponseText.Should().Contain(maxSizeValue);
    }

    [Fact]
    public async Task ExceedMaxKeys_ShouldTriggerEviction()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var keys = new List<string>();

        // Fill cache to maximum capacity (MaxKeys = 10)
        for (int i = 1; i <= 10; i++)
        {
            var key = $"limit_key_{i}";
            var value = $"value_{i}";
            keys.Add(key);

            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            
            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
            responseText.Should().Contain("STORED");
        }

        // Act - Add one more key to exceed the limit
        var extraKey = "extra_key";
        var extraValue = "extra_value";
        var extraSetCommand = $"set {extraKey} 0 0 {extraValue.Length}\r\n{extraValue}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(extraSetCommand));
        
        var extraResponse = new byte[1024];
        var extraBytesRead = await stream.ReadAsync(extraResponse);
        var extraResponseText = Encoding.UTF8.GetString(extraResponse, 0, extraBytesRead);

        // Assert - Either the new key is stored (with eviction) or rejected
        // Depending on implementation, this could be STORED (with eviction) or an error
        extraResponseText.Should().Match(response => 
            response.Contains("STORED") || response.Contains("SERVER_ERROR"));

        if (extraResponseText.Contains("STORED"))
        {
            // If stored, verify the new key is accessible
            var getExtraCommand = $"get {extraKey}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(getExtraCommand));
            var getExtraResponse = new byte[1024];
            var getExtraBytesRead = await stream.ReadAsync(getExtraResponse);
            var getExtraResponseText = Encoding.UTF8.GetString(getExtraResponse, 0, getExtraBytesRead);
            getExtraResponseText.Should().Contain($"VALUE {extraKey}");
            
            // And at least one original key should have been evicted
            var allKeysPresent = true;
            foreach (var originalKey in keys)
            {
                var checkCommand = $"get {originalKey}\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(checkCommand));
                var checkResponse = new byte[1024];
                var checkBytesRead = await stream.ReadAsync(checkResponse);
                var checkResponseText = Encoding.UTF8.GetString(checkResponse, 0, checkBytesRead);
                
                if (!checkResponseText.Contains($"VALUE {originalKey}"))
                {
                    allKeysPresent = false;
                    break;
                }
            }
            allKeysPresent.Should().BeFalse("At least one original key should have been evicted");
        }
    }

    [Fact]
    public async Task EmptyKey_ShouldBeRejected()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var value = "test_value";

        // Act - Try to set with empty key
        var setCommand = $"set  0 0 {value.Length}\r\n{value}\r\n"; // Empty key
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Empty keys should be rejected");
    }

    [Fact]
    public async Task ZeroLengthValue_ShouldBeAccepted()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "empty_value_key";
        var value = ""; // Empty value

        // Act
        var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("STORED", "Zero-length values should be accepted");

        // Verify we can retrieve it
        var getCommand = $"get {key}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
        var getResponse = new byte[1024];
        var getBytesRead = await stream.ReadAsync(getResponse);
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);
        getResponseText.Should().Contain($"VALUE {key} 0 0");
        getResponseText.Should().Contain("END");
    }

    [Fact]
    public async Task MultipleSmallValues_ShouldRespectTotalSizeLimit()
    {
        // This test verifies behavior when approaching total cache size limits
        // Note: Actual total size enforcement would depend on server implementation
        
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Calculate how many 100-byte values we can store before hitting limits
        var valueSize = 100;
        var testValue = new string('z', valueSize);
        var keysStored = 0;

        // Act - Store values until we hit a limit
        for (int i = 1; i <= 20; i++) // Try to store more than MaxKeys
        {
            var key = $"size_test_{i}";
            var setCommand = $"set {key} 0 0 {testValue.Length}\r\n{testValue}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            
            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
            
            if (responseText.Contains("STORED"))
            {
                keysStored++;
            }
            else if (responseText.Contains("SERVER_ERROR"))
            {
                // Hit a size limit
                break;
            }
        }

        // Assert - Should have stored at most MaxKeys (10) due to key limit
        keysStored.Should().BeLessOrEqualTo(10, "Should not exceed MaxKeys limit");
    }

    [Fact]
    public async Task SpecialCharactersInKey_ShouldBeHandledCorrectly()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Test various special characters that might be problematic
        var testCases = new Dictionary<string, bool>
        {
            { "key_with_underscores", true },
            { "key-with-dashes", true },
            { "key.with.dots", true },
            { "key123numbers", true },
            { "key with spaces", false }, // Spaces should not be allowed
            { "key\twith\ttab", false }, // Control characters should not be allowed
            { "key\nwith\nnewline", false } // Newlines should not be allowed
        };

        foreach (var testCase in testCases)
        {
            var key = testCase.Key;
            var shouldSucceed = testCase.Value;
            var value = "test_value";

            // Act
            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
            await stream.WriteAsync(setCommandBytes);

            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

            // Assert
            if (shouldSucceed)
            {
                responseText.Should().Contain("STORED", $"Key '{key}' should be accepted");
            }
            else
            {
                responseText.Should().Contain("CLIENT_ERROR", $"Key '{key}' should be rejected");
            }
        }
    }
}