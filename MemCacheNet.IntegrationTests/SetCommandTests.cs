using System.Net.Sockets;
using System.Text;

namespace MemCacheNet.IntegrationTests;

public class SetCommandTests : BaseIntegrationTest
{
    [Fact]
    public async Task SetCommand_WithValidData_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "testkey1";
        var value = "testvalue1";

        // Act
        var response = await SendSetCommandAsync(key, value);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithEmptyValue_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "emptykey";
        var value = "";

        // Act
        var response = await SendSetCommandAsync(key, value);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithLargeValue_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "largekey";
        var value = new string('x', 1000); // 1KB value

        // Act
        var response = await SendSetCommandAsync(key, value);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithSpecialCharacters_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "specialkey";
        var value = "Hello\nWorld\r\nWith\tTabs";

        // Act
        var response = await SendSetCommandAsync(key, value);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithBinaryData_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "binarykey";
        var value = "Binary\0Data\xFF\xFE";

        // Act
        var response = await SendSetCommandAsync(key, value);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithCustomFlags_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "flaggedkey";
        var value = "flaggedvalue";
        var flags = (uint)123;

        // Act
        var response = await SendSetCommandAsync(key, value, flags);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithExpiration_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "expirekey";
        var value = "expirevalue";
        var expiration = 3600; // 1 hour

        // Act
        var response = await SendSetCommandAsync(key, value, 0, expiration);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_MultipleValues_ShouldStoreAllValues()
    {
        // Arrange
        await ConnectAsync();
        var testData = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2", 
            ["key3"] = "value3",
            ["key4"] = "value4",
            ["key5"] = "value5"
        };

        // Act & Assert
        foreach (var (key, value) in testData)
        {
            var response = await SendSetCommandAsync(key, value);
            response.Should().Contain("STORED", $"Failed to store {key}={value}");
        }
    }

    [Fact]
    public async Task SetCommand_OverwriteExistingKey_ShouldReturnStoredResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "overwritekey";
        var originalValue = "original";
        var newValue = "updated";

        // Act
        var firstResponse = await SendSetCommandAsync(key, originalValue);
        var secondResponse = await SendSetCommandAsync(key, newValue);

        // Assert
        firstResponse.Should().Contain("STORED");
        secondResponse.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_WithNoReply_ShouldNotReturnResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "noreplykey";
        var value = "noreplyvalue";
        var command = $"set {key} 0 0 {value.Length} noreply\r\n{value}\r\n";

        // Act
        var response = await SendNoReplyCommandAsync(command);

        // Assert
        response.Should().BeEmpty();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("mediumlengthvalue")]
    [InlineData("verylongvaluewithalotofcharacterstotesthandlingoflargerstrings")]
    public async Task SetCommand_VariousValueLengths_ShouldReturnStoredResponse(string value)
    {
        // Arrange
        await ConnectAsync();
        var key = $"lengthtest_{value.Length}";

        // Act
        var response = await SendSetCommandAsync(key, value);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task SetCommand_ConcurrentSets_ShouldAllSucceed()
    {
        // Arrange
        var tasks = new List<Task<string>>();
        var concurrentCount = 10;

        // Act
        for (int i = 0; i < concurrentCount; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                // Each concurrent task needs its own connection
                using var client = new TcpClient();
                await client.ConnectAsync(DefaultHost, DefaultPort);
                using var stream = client.GetStream();
                
                var key = $"concurrent_{taskId}";
                var value = $"value_{taskId}";
                var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
                
                var commandBytes = Encoding.UTF8.GetBytes(setCommand);
                await stream.WriteAsync(commandBytes);

                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            response.Should().Contain("STORED");
        }
    }
}