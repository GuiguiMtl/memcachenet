using System.Text;

namespace MemCacheNet.IntegrationTests;

public class EdgeCaseAndErrorTests : BaseIntegrationTest
{
    [Fact]
    public async Task InvalidCommand_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var invalidCommand = "invalid_command test\r\n";

        // Act
        var response = await SendCommandAsync(invalidCommand);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_MissingParameters_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var incompleteCommand = "set key1\r\n";

        // Act
        var response = await SendCommandAsync(incompleteCommand);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_InvalidFlags_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 invalid_flags 0 5\r\nvalue\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_InvalidExpiration_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 0 invalid_exp 5\r\nvalue\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_InvalidDataLength_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 0 0 invalid_length\r\nvalue\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_DataLengthMismatch_TooShort_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 0 0 10\r\nshort\r\n"; // Says 10 bytes but only provides 5

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR|BAD_DATA_CHUNK");
    }

    [Fact]
    public async Task SetCommand_DataLengthMismatch_TooLong_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 0 0 3\r\ntoolong\r\n"; // Says 3 bytes but provides 7

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR|BAD_DATA_CHUNK");
    }

    [Fact]
    public async Task SetCommand_NegativeDataLength_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 0 0 -5\r\nvalue\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_ExtremelyLargeDataLength_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set key1 0 0 999999999\r\nvalue\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR|SERVER_ERROR");
    }

    [Fact]
    public async Task SetCommand_EmptyKey_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "set  0 0 5\r\nvalue\r\n"; // Empty key

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_TooLargeKey_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var largeKey = new string('k', 1000); // Very large key
        var command = $"set {largeKey} 0 0 5\r\nvalue\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task GetCommand_EmptyKey_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "get \r\n"; // Empty key

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR|END");
    }

    [Fact]
    public async Task GetCommand_TooLargeKey_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var largeKey = new string('k', 1000); // Very large key
        var command = $"get {largeKey}\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task DeleteCommand_EmptyKey_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var command = "delete \r\n"; // Empty key

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task DeleteCommand_TooLargeKey_ShouldReturnError()
    {
        // Arrange
        await ConnectAsync();
        var largeKey = new string('k', 1000); // Very large key
        var command = $"delete {largeKey}\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_WithOnlyCR_ShouldHandleGracefully()
    {
        // Arrange
        await ConnectAsync();
        var command = "set test 0 0 5\rvalue\r"; // Only \r instead of \r\n

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_WithOnlyLF_ShouldHandleGracefully()
    {
        // Arrange
        await ConnectAsync();
        var command = "set test 0 0 5\nvalue\n"; // Only \n instead of \r\n

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_MaxValidValues_ShouldSucceed()
    {
        // Arrange
        await ConnectAsync();
        var key = new string('k', 250); // Maximum allowed key size
        var flags = uint.MaxValue; // Maximum flags value
        var expiration = int.MaxValue; // Maximum expiration
        var value = "validvalue";

        // Act
        var response = await SendSetCommandAsync(key, value, flags, expiration);

        // Assert
        response.Should().Contain("STORED");
    }

    [Fact]
    public async Task Commands_RapidSuccession_ShouldHandleAll()
    {
        // Arrange
        await ConnectAsync();
        var commandCount = 100;
        var responses = new List<string>();

        // Act
        for (int i = 0; i < commandCount; i++)
        {
            var key = $"rapid_{i}";
            var value = $"value_{i}";
            var response = await SendSetCommandAsync(key, value);
            responses.Add(response);
        }

        // Assert
        foreach (var response in responses)
        {
            response.Should().Contain("STORED");
        }
    }

    [Fact]
    public async Task SetCommand_UnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        await ConnectAsync();
        var key = "unicode_test";
        var value = "Hello 世界 🌍 Здравствуй мир"; // Mixed Unicode

        // Act
        var setResponse = await SendSetCommandAsync(key, value);
        var getResponse = await SendGetCommandAsync(key);

        // Assert
        setResponse.Should().Contain("STORED");
        getResponse.Should().Contain(value);
    }

    [Fact]
    public async Task Commands_VeryLongParameterString_ShouldHandleGracefully()
    {
        // Arrange
        await ConnectAsync();
        var longParameters = string.Join(" ", Enumerable.Repeat("param", 1000));
        var command = $"invalid_command {longParameters}\r\n";

        // Act
        var response = await SendCommandAsync(command);

        // Assert
        response.Should().MatchRegex("ERROR|CLIENT_ERROR");
    }

    [Fact]
    public async Task SetCommand_ZeroLengthData_ShouldSucceed()
    {
        // Arrange
        await ConnectAsync();
        var key = "zerodata";
        var value = "";

        // Act
        var setResponse = await SendSetCommandAsync(key, value);
        var getResponse = await SendGetCommandAsync(key);

        // Assert
        setResponse.Should().Contain("STORED");
        getResponse.Should().Contain($"VALUE {key} 0 0");
        getResponse.Should().Contain("END");
    }

    [Theory]
    [InlineData("simple_key")]
    [InlineData("key-with-dashes")]
    [InlineData("key_with_underscores")]
    [InlineData("key.with.dots")]
    [InlineData("key123with456numbers")]
    [InlineData("MixedCaseKey")]
    public async Task SetAndGet_VariousKeyFormats_ShouldWorkCorrectly(string key)
    {
        // Arrange
        await ConnectAsync();
        var value = $"value_for_{key}";

        // Act
        var setResponse = await SendSetCommandAsync(key, value);
        var getResponse = await SendGetCommandAsync(key);

        // Assert
        setResponse.Should().Contain("STORED");
        getResponse.Should().Contain($"VALUE {key} 0 {value.Length}");
        getResponse.Should().Contain(value);
    }

    [Fact]
    public async Task Commands_WithoutProperCRLF_ShouldTimeoutGracefully()
    {
        // Arrange
        await ConnectAsync();
        var incompleteCommand = "get test"; // Missing \r\n

        // Act - Send incomplete command and measure response time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await SendCommandAsync(incompleteCommand);
        stopwatch.Stop();
        
        // Assert - Server should close connection (empty response) within timeout period
        // The server has ReadTimeoutSeconds = 5, so should close quickly
        response.Should().BeEmpty("Server should close connection due to incomplete command");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(11000, "Should timeout within reasonable time");
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(4000, "Should wait for configured timeout period");
    }
}