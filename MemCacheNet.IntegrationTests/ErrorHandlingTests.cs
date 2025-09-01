using System.Net.Sockets;
using System.Text;
using FluentAssertions;

namespace MemCacheNet.IntegrationTests;

/// <summary>
/// Integration tests for error handling and edge cases in the MemCache server.
/// </summary>
public class ErrorHandlingTests
{
    public int TestPort = 11211;

    public ErrorHandlingTests()
    {
    }

    [Fact]
    public async Task MalformedSetCommand_MissingParameters_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act - Send SET command with missing parameters
        var malformedCommand = "set incomplete_command\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(malformedCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Malformed SET command should return error");
    }

    [Fact]
    public async Task SetCommandWithInvalidFlags_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "test_key";
        var value = "test_value";

        // Act - Send SET command with invalid flags (non-numeric)
        var invalidCommand = $"set {key} invalid_flags 0 {value.Length}\r\n{value}\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(invalidCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Invalid flags should return error");
    }

    [Fact]
    public async Task SetCommandWithInvalidExpiration_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "test_key";
        var value = "test_value";

        // Act - Send SET command with invalid expiration (non-numeric)
        var invalidCommand = $"set {key} 0 invalid_expiration {value.Length}\r\n{value}\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(invalidCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Invalid expiration should return error");
    }

    [Fact]
    public async Task SetCommandWithInvalidLength_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "test_key";
        var value = "test_value";

        // Act - Send SET command with invalid length (non-numeric)
        var invalidCommand = $"set {key} 0 0 invalid_length\r\n{value}\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(invalidCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Invalid length should return error");
    }

    [Fact]
    public async Task SetCommandWithMismatchedDataLength_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "test_key";
        var value = "test_value"; // 10 characters
        var wrongLength = 15; // Claiming 15 characters

        // Act - Send SET command with mismatched length
        var mismatchedCommand = $"set {key} 0 0 {wrongLength}\r\n{value}\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(mismatchedCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Match(text => 
            text.Contains("CLIENT_ERROR") || text.Contains("SERVER_ERROR"), 
            "Mismatched data length should return error");
    }

    [Fact]
    public async Task DeleteNonExistentKey_ShouldReturnNotFound()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var nonExistentKey = "definitely_does_not_exist";

        // Act
        var deleteCommand = $"delete {nonExistentKey}\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(deleteCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("NOT_FOUND", "Deleting non-existent key should return NOT_FOUND");
    }

    [Fact]
    public async Task MalformedDeleteCommand_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act - Send DELETE command without key
        var malformedCommand = "delete\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(malformedCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Malformed DELETE command should return error");
    }

    [Fact]
    public async Task MalformedGetCommand_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act - Send GET command without key
        var malformedCommand = "get\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(malformedCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Malformed GET command should return error");
    }

    [Fact]
    public async Task CommandWithoutCRLF_ShouldBeHandledGracefully()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act - Send command without proper CRLF termination
        var commandWithoutCRLF = "get test_key"; // Missing \r\n
        var commandBytes = Encoding.UTF8.GetBytes(commandWithoutCRLF);
        await stream.WriteAsync(commandBytes);

        // Wait a bit to see if server responds or handles this gracefully
        await Task.Delay(100);

        // Send a proper command to verify connection is still working
        var properCommand = "\r\nget another_key\r\n";
        var properCommandBytes = Encoding.UTF8.GetBytes(properCommand);
        await stream.WriteAsync(properCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert - Server should handle this gracefully and respond to proper command
        responseText.Should().Contain("END", "Server should handle incomplete commands gracefully");
    }

    [Fact]
    public async Task VeryLongInvalidCommand_ShouldReturnError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act - Send a very long invalid command
        var longInvalidCommand = "invalid_command_" + new string('x', 1000) + "\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(longInvalidCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[2048]; // Larger buffer for potential long response
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR", "Very long invalid command should return error");
    }

    [Fact]
    public async Task ClientDisconnectDuringCommand_ShouldNotCrashServer()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act - Start sending a SET command but disconnect before completing
        var partialCommand = "set test_key 0 0 100\r\npartial_data";
        var commandBytes = Encoding.UTF8.GetBytes(partialCommand);
        await stream.WriteAsync(commandBytes);

        // Immediately close the connection
        client.Close();

        // Wait a bit for server to process
        await Task.Delay(100);

        // Assert - Server should still be running, verify with new connection
        using var newClient = new TcpClient();
        await newClient.ConnectAsync("127.0.0.1", TestPort);
        using var newStream = newClient.GetStream();

        var testCommand = "get test_verification\r\n";
        await newStream.WriteAsync(Encoding.UTF8.GetBytes(testCommand));
        
        var verificationResponse = new byte[1024];
        var verificationBytesRead = await newStream.ReadAsync(verificationResponse);
        var verificationResponseText = Encoding.UTF8.GetString(verificationResponse, 0, verificationBytesRead);

        verificationResponseText.Should().Contain("END", "Server should still be responsive after client disconnect");
    }

    [Fact]
    public async Task BinaryDataInValue_ShouldBeHandledCorrectly()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "binary_data_key";
        // Create binary data with null bytes and control characters
        var binaryData = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0x0A, 0x0D, 0x20 };

        // Act - Set binary data
        var setCommandPrefix = $"set {key} 0 0 {binaryData.Length}\r\n";
        var setCommandSuffix = "\r\n";
        
        var commandBytes = Encoding.UTF8.GetBytes(setCommandPrefix)
            .Concat(binaryData)
            .Concat(Encoding.UTF8.GetBytes(setCommandSuffix))
            .ToArray();

        await stream.WriteAsync(commandBytes);

        var setResponse = new byte[1024];
        var setBytesRead = await stream.ReadAsync(setResponse);
        var setResponseText = Encoding.UTF8.GetString(setResponse, 0, setBytesRead);

        // Assert - Should store successfully
        setResponseText.Should().Contain("STORED", "Binary data should be stored successfully");

        // Verify retrieval
        var getCommand = $"get {key}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));

        var getResponse = new byte[1024];
        var getBytesRead = await stream.ReadAsync(getResponse);

        // The response should contain the VALUE header and the binary data
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);
        getResponseText.Should().Contain($"VALUE {key} 0 {binaryData.Length}");
        getResponseText.Should().Contain("END");
    }

    [Fact]
    public async Task ConcurrentErrorConditions_ShouldNotAffectOtherClients()
    {
        // Arrange - Create multiple clients, some will send invalid commands
        var validClientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", TestPort);
            using var stream = client.GetStream();

            // Valid operations
            var setCommand = "set valid_key 0 0 10\r\nvalid_data\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            var setResponse = new byte[1024];
            await stream.ReadAsync(setResponse);

            var getCommand = "get valid_key\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
            var getResponse = new byte[1024];
            var bytesRead = await stream.ReadAsync(getResponse);
            var responseText = Encoding.UTF8.GetString(getResponse, 0, bytesRead);

            return responseText.Contains("VALUE valid_key");
        });

        var invalidClientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", TestPort);
            using var stream = client.GetStream();

            // Invalid operations
            var invalidCommand = "completely_invalid_command\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(invalidCommand));
            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

            return responseText.Contains("CLIENT_ERROR");
        });

        // Act
        var results = await Task.WhenAll(validClientTask, invalidClientTask);

        // Assert
        results[0].Should().BeTrue("Valid client operations should succeed");
        results[1].Should().BeTrue("Invalid client should receive error response");
    }
}