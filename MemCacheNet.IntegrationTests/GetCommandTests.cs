using System.Text;

namespace MemCacheNet.IntegrationTests;

public class GetCommandTests : BaseIntegrationTest
{
    [Fact]
    public async Task GetCommand_ExistingKey_ShouldReturnValue()
    {
        // Arrange
        await ConnectAsync();
        var key = "gettest1";
        var value = "testvalue1";
        
        // First, set the value
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().Contain($"VALUE {key} 0 {value.Length}");
        response.Should().Contain(value);
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_NonExistentKey_ShouldReturnEndOnly()
    {
        // Arrange
        await ConnectAsync();
        var key = "nonexistent";

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().NotContain("VALUE");
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_EmptyValue_ShouldReturnCorrectResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "emptyvalue";
        var value = "";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().Contain($"VALUE {key} 0 0");
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_WithCustomFlags_ShouldReturnFlags()
    {
        // Arrange
        await ConnectAsync();
        var key = "flagtest";
        var value = "flagvalue";
        uint flags = 456;
        
        await SendSetCommandAsync(key, value, flags);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().Contain($"VALUE {key} {flags} {value.Length}");
        response.Should().Contain(value);
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_LargeValue_ShouldReturnCompleteValue()
    {
        // Arrange
        await ConnectAsync();
        var key = "largeget";
        var value = new string('A', 2000); // 2KB value
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().Contain($"VALUE {key} 0 {value.Length}");
        response.Should().Contain(value);
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_BinaryData_ShouldReturnCorrectData()
    {
        // Arrange
        await ConnectAsync();
        var key = "binaryget";
        var value = "Binary\0Data\xFF\xFE";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        var bytesCount = Encoding.UTF8.GetByteCount(value);
        response.Should().Contain($"VALUE {key} 0 {bytesCount}");
        response.Should().Contain(value);
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_MultipleKeys_AllExist_ShouldReturnAllValues()
    {
        // Arrange
        await ConnectAsync();
        var testData = new Dictionary<string, string>
        {
            ["multi1"] = "value1",
            ["multi2"] = "value2",
            ["multi3"] = "value3"
        };

        // Set all values
        foreach (var (key, value) in testData)
        {
            await SendSetCommandAsync(key, value);
        }

        // Act
        var response = await SendGetCommandAsync(testData.Keys.ToArray());

        // Assert
        foreach (var (key, value) in testData)
        {
            response.Should().Contain($"VALUE {key} 0 {value.Length}");
            response.Should().Contain(value);
        }
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_MultipleKeys_SomeExist_ShouldReturnOnlyExisting()
    {
        // Arrange
        await ConnectAsync();
        var existingKey = "exists";
        var existingValue = "existingvalue";
        var nonExistentKey = "doesnotexist";
        
        await SendSetCommandAsync(existingKey, existingValue);

        // Act
        var response = await SendGetCommandAsync(existingKey, nonExistentKey);

        // Assert
        response.Should().Contain($"VALUE {existingKey} 0 {existingValue.Length}");
        response.Should().Contain(existingValue);
        response.Should().NotContain($"VALUE {nonExistentKey}");
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_MultipleKeys_NoneExist_ShouldReturnEndOnly()
    {
        // Arrange
        await ConnectAsync();
        var keys = new[] { "none1", "none2", "none3" };

        // Act
        var response = await SendGetCommandAsync(keys);

        // Assert
        response.Should().NotContain("VALUE");
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_RepeatedKey_ShouldReturnOnlyOnce()
    {
        // Arrange
        await ConnectAsync();
        var key = "repeated";
        var value = "repeatedvalue";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendGetCommandAsync(key, key, key);

        // Assert - Should only appear once in response
        var valueOccurrences = CountOccurrences(response, $"VALUE {key} 0 {value.Length}");
        valueOccurrences.Should().Be(1);
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_AfterSetAndOverwrite_ShouldReturnLatestValue()
    {
        // Arrange
        await ConnectAsync();
        var key = "overwrite";
        var originalValue = "original";
        var updatedValue = "updated";
        
        await SendSetCommandAsync(key, originalValue);
        await SendSetCommandAsync(key, updatedValue);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().Contain($"VALUE {key} 0 {updatedValue.Length}");
        response.Should().Contain(updatedValue);
        response.Should().NotContain(originalValue);
        response.Should().Contain("END");
    }

    [Fact]
    public async Task GetCommand_ManyKeys_ShouldHandleLargeRequest()
    {
        // Arrange
        await ConnectAsync();
        var keyCount = 50;
        var keys = new List<string>();
        
        // Set many values
        for (int i = 0; i < keyCount; i++)
        {
            var key = $"bulk_{i}";
            var value = $"value_{i}";
            keys.Add(key);
            await SendSetCommandAsync(key, value);
        }

        // Act
        var response = await SendGetCommandAsync(keys.ToArray());

        // Assert
        for (int i = 0; i < keyCount; i++)
        {
            var key = $"bulk_{i}";
            var value = $"value_{i}";
            response.Should().Contain($"VALUE {key} 0 {value.Length}");
            response.Should().Contain(value);
        }
        response.Should().Contain("END");
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("with-dashes")]
    [InlineData("with_underscores")]
    [InlineData("with123numbers")]
    [InlineData("MixedCase")]
    public async Task GetCommand_VariousKeyFormats_ShouldReturnValues(string key)
    {
        // Arrange
        await ConnectAsync();
        var value = $"value_for_{key}";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendGetCommandAsync(key);

        // Assert
        response.Should().Contain($"VALUE {key} 0 {value.Length}");
        response.Should().Contain(value);
        response.Should().Contain("END");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        
        return count;
    }
}