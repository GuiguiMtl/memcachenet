using System.Net.Sockets;
using System.Text;
using FluentAssertions;

namespace MemCacheNet.IntegrationTests;

/// <summary>
/// Integration tests for the MemCache server protocol implementation.
/// Tests the complete stack including TCP communication, command parsing, and response formatting.
/// </summary>
public class MemCacheProtocolTests : IntegrationTestBase
{
    public MemCacheProtocolTests() : base(11212) // Use different port to avoid conflicts
    {
    }

    [Fact]
    public async Task SetAndGet_ShouldStoreAndRetrieveValue()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "test_key";
        var value = "test_value";
        var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";

        // Act - Set value
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);
        
        var setResponse = new byte[1024];
        var setBytesRead = await stream.ReadAsync(setResponse);
        var setResponseText = Encoding.UTF8.GetString(setResponse, 0, setBytesRead);

        // Assert - Set response
        setResponseText.Should().Contain("STORED");

        // Act - Get value
        var getCommand = $"get {key}\r\n";
        var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
        await stream.WriteAsync(getCommandBytes);

        var getResponse = new byte[1024];
        var getBytesRead = await stream.ReadAsync(getResponse);
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);

        // Assert - Get response
        getResponseText.Should().Contain($"VALUE {key} 0 {value.Length}");
        getResponseText.Should().Contain(value);
        getResponseText.Should().Contain("END");
    }

    [Fact]
    public async Task SetAndDelete_ShouldRemoveValue()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "delete_test_key";
        var value = "delete_test_value";
        var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";

        // Act - Set value
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);
        
        var setResponse = new byte[1024];
        await stream.ReadAsync(setResponse);

        // Act - Delete value
        var deleteCommand = $"delete {key}\r\n";
        var deleteCommandBytes = Encoding.UTF8.GetBytes(deleteCommand);
        await stream.WriteAsync(deleteCommandBytes);

        var deleteResponse = new byte[1024];
        var deleteBytesRead = await stream.ReadAsync(deleteResponse);
        var deleteResponseText = Encoding.UTF8.GetString(deleteResponse, 0, deleteBytesRead);

        // Assert - Delete response
        deleteResponseText.Should().Contain("DELETED");

        // Act - Try to get deleted value
        var getCommand = $"get {key}\r\n";
        var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
        await stream.WriteAsync(getCommandBytes);

        var getResponse = new byte[1024];
        var getBytesRead = await stream.ReadAsync(getResponse);
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);

        // Assert - Get response should only contain END (no value)
        getResponseText.Should().NotContain($"VALUE {key}");
        getResponseText.Should().Contain("END");
    }

    [Fact]
    public async Task GetNonExistentKey_ShouldReturnEndOnly()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "non_existent_key";

        // Act
        var getCommand = $"get {key}\r\n";
        var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
        await stream.WriteAsync(getCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().NotContain($"VALUE {key}");
        responseText.Should().Contain("END");
    }

    [Fact]
    public async Task SetWithNoReply_ShouldNotReturnResponse()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "noreply_key";
        var value = "noreply_value";
        var setCommand = $"set {key} 0 0 {value.Length} noreply\r\n{value}\r\n";

        // Act
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);
        
        // Wait a bit to see if any response comes
        await Task.Delay(100);
        
        // Check if any data is available
        var available = client.Available;
        
        // Assert
        available.Should().Be(0, "No response should be sent with noreply flag");
    }

    [Fact]
    public async Task DeleteWithNoReply_ShouldNotReturnResponse()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key = "noreply_delete_key";
        var value = "test_value";

        // First set the value
        var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);
        
        var setResponse = new byte[1024];
        await stream.ReadAsync(setResponse);

        // Act - Delete with noreply
        var deleteCommand = $"delete {key} noreply\r\n";
        var deleteCommandBytes = Encoding.UTF8.GetBytes(deleteCommand);
        await stream.WriteAsync(deleteCommandBytes);

        // Wait a bit to see if any response comes
        await Task.Delay(100);
        
        // Check if any data is available
        var available = client.Available;
        
        // Assert
        available.Should().Be(0, "No response should be sent with noreply flag");
    }

    [Fact]
    public async Task InvalidCommand_ShouldReturnClientError()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Act
        var invalidCommand = "invalid_command\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(invalidCommand);
        await stream.WriteAsync(commandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain("CLIENT_ERROR");
    }

    [Fact]
    public async Task GetMultipleKeys_ShouldReturnAllExistingValues()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        var key1 = "multi_key1";
        var value1 = "value1";
        var key2 = "multi_key2";
        var value2 = "value2";
        var key3 = "non_existent";

        // Set two values
        var setCommand1 = $"set {key1} 0 0 {value1.Length}\r\n{value1}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand1));
        await stream.ReadAsync(new byte[1024]); // Read response

        var setCommand2 = $"set {key2} 0 0 {value2.Length}\r\n{value2}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand2));
        await stream.ReadAsync(new byte[1024]); // Read response

        // Act - Get multiple keys
        var getCommand = $"get {key1} {key2} {key3}\r\n";
        var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
        await stream.WriteAsync(getCommandBytes);

        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);

        // Assert
        responseText.Should().Contain($"VALUE {key1} 0 {value1.Length}");
        responseText.Should().Contain(value1);
        responseText.Should().Contain($"VALUE {key2} 0 {value2.Length}");
        responseText.Should().Contain(value2);
        responseText.Should().NotContain($"VALUE {key3}");
        responseText.Should().Contain("END");
    }
}