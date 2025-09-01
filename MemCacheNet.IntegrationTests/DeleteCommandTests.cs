namespace MemCacheNet.IntegrationTests;

public class DeleteCommandTests : BaseIntegrationTest
{
    [Fact]
    public async Task DeleteCommand_ExistingKey_ShouldReturnDeleted()
    {
        // Arrange
        await ConnectAsync();
        var key = "deletetest1";
        var value = "testvalue1";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendDeleteCommandAsync(key);

        // Assert
        response.Should().Contain("DELETED");
    }

    [Fact]
    public async Task DeleteCommand_NonExistentKey_ShouldReturnNotFound()
    {
        // Arrange
        await ConnectAsync();
        var key = "nonexistent";

        // Act
        var response = await SendDeleteCommandAsync(key);

        // Assert
        response.Should().Contain("NOT_FOUND");
    }

    [Fact]
    public async Task DeleteCommand_ExistingKeyWithNoReply_ShouldNotReturnResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "deletenoreply";
        var value = "testvalue";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendDeleteCommandAsync(key, noReply: true);

        // Assert
        response.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_NonExistentKeyWithNoReply_ShouldNotReturnResponse()
    {
        // Arrange
        await ConnectAsync();
        var key = "nonexistentnoreply";

        // Act
        var response = await SendDeleteCommandAsync(key, noReply: true);

        // Assert
        response.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_AfterDelete_GetShouldReturnNotFound()
    {
        // Arrange
        await ConnectAsync();
        var key = "deleteverify";
        var value = "testvalue";
        
        await SendSetCommandAsync(key, value);
        var beforeDelete = await SendGetCommandAsync(key);

        // Act
        await SendDeleteCommandAsync(key);
        var afterDelete = await SendGetCommandAsync(key);

        // Assert
        beforeDelete.Should().Contain($"VALUE {key} 0 {value.Length}");
        beforeDelete.Should().Contain(value);
        
        afterDelete.Should().NotContain("VALUE");
        afterDelete.Should().Contain("END");
    }

    [Fact]
    public async Task DeleteCommand_ThenSet_ShouldAllowNewValue()
    {
        // Arrange
        await ConnectAsync();
        var key = "deletesettest";
        var originalValue = "original";
        var newValue = "new";
        
        await SendSetCommandAsync(key, originalValue);
        await SendDeleteCommandAsync(key);

        // Act
        var setResponse = await SendSetCommandAsync(key, newValue);
        var getResponse = await SendGetCommandAsync(key);

        // Assert
        setResponse.Should().Contain("STORED");
        getResponse.Should().Contain($"VALUE {key} 0 {newValue.Length}");
        getResponse.Should().Contain(newValue);
        getResponse.Should().NotContain(originalValue);
    }

    [Fact]
    public async Task DeleteCommand_MultipleKeys_ShouldDeleteEachIndividually()
    {
        // Arrange
        await ConnectAsync();
        var testData = new Dictionary<string, string>
        {
            ["del1"] = "value1",
            ["del2"] = "value2",
            ["del3"] = "value3"
        };

        // Set all values
        foreach (var (key, value) in testData)
        {
            await SendSetCommandAsync(key, value);
        }

        // Act & Assert
        foreach (var key in testData.Keys)
        {
            var deleteResponse = await SendDeleteCommandAsync(key);
            deleteResponse.Should().Contain("DELETED");
            
            var getResponse = await SendGetCommandAsync(key);
            getResponse.Should().NotContain("VALUE");
            getResponse.Should().Contain("END");
        }
    }

    [Fact]
    public async Task DeleteCommand_SameKeyTwice_ShouldReturnNotFoundSecondTime()
    {
        // Arrange
        await ConnectAsync();
        var key = "doubledel";
        var value = "testvalue";
        
        await SendSetCommandAsync(key, value);

        // Act
        var firstDelete = await SendDeleteCommandAsync(key);
        var secondDelete = await SendDeleteCommandAsync(key);

        // Assert
        firstDelete.Should().Contain("DELETED");
        secondDelete.Should().Contain("NOT_FOUND");
    }

    [Fact]
    public async Task DeleteCommand_WithDifferentKeyFormats_ShouldWork()
    {
        // Arrange
        await ConnectAsync();
        var keys = new[]
        {
            "simple",
            "with-dashes",
            "with_underscores", 
            "with123numbers",
            "MixedCase"
        };

        // Set all values
        foreach (var key in keys)
        {
            await SendSetCommandAsync(key, $"value_{key}");
        }

        // Act & Assert
        foreach (var key in keys)
        {
            var response = await SendDeleteCommandAsync(key);
            response.Should().Contain("DELETED");
        }
    }

    [Fact]
    public async Task DeleteCommand_LargeKey_ShouldWork()
    {
        // Arrange
        await ConnectAsync();
        var key = new string('k', 200); // Large but valid key
        var value = "largeKeyValue";
        
        await SendSetCommandAsync(key, value);

        // Act
        var response = await SendDeleteCommandAsync(key);

        // Assert
        response.Should().Contain("DELETED");
    }

    [Fact]
    public async Task DeleteCommand_ConcurrentDeletes_ShouldHandleGracefully()
    {
        // Arrange
        var concurrentCount = 10;
        var tasks = new List<Task<string>>();

        // Create unique keys for each concurrent operation
        for (int i = 0; i < concurrentCount; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                await ConnectAsync();
                var key = $"concurrent_del_{taskId}";
                var value = $"value_{taskId}";
                
                await SendSetCommandAsync(key, value);
                return await SendDeleteCommandAsync(key);
            }));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            response.Should().Contain("DELETED");
        }
    }

    [Fact]
    public async Task DeleteCommand_ConcurrentDeletesSameKey_ShouldHandleGracefully()
    {
        // Arrange
        await ConnectAsync();
        var key = "race_condition_key";
        var value = "race_value";
        
        await SendSetCommandAsync(key, value);

        var concurrentCount = 5;
        var tasks = new List<Task<string>>();

        // Act - Multiple concurrent deletes of same key
        for (int i = 0; i < concurrentCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await ConnectAsync();
                return await SendDeleteCommandAsync(key);
            }));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - Only one should succeed with DELETED, others should get NOT_FOUND
        var deletedCount = responses.Count(r => r.Contains("DELETED"));
        var notFoundCount = responses.Count(r => r.Contains("NOT_FOUND"));
        
        deletedCount.Should().Be(1);
        notFoundCount.Should().Be(concurrentCount - 1);
    }

    [Fact]
    public async Task DeleteCommand_InterleavedWithSetsAndGets_ShouldMaintainConsistency()
    {
        // Arrange
        await ConnectAsync();
        var key = "consistency";
        var value1 = "value1";
        var value2 = "value2";

        // Act & Assert - Complex sequence
        // Set -> Get -> Delete -> Get -> Set -> Get -> Delete
        
        await SendSetCommandAsync(key, value1);
        var get1 = await SendGetCommandAsync(key);
        get1.Should().Contain(value1);
        
        var delete1 = await SendDeleteCommandAsync(key);
        delete1.Should().Contain("DELETED");
        
        var get2 = await SendGetCommandAsync(key);
        get2.Should().NotContain("VALUE");
        
        await SendSetCommandAsync(key, value2);
        var get3 = await SendGetCommandAsync(key);
        get3.Should().Contain(value2);
        
        var delete2 = await SendDeleteCommandAsync(key);
        delete2.Should().Contain("DELETED");
        
        var get4 = await SendGetCommandAsync(key);
        get4.Should().NotContain("VALUE");
    }

    [Theory]
    [InlineData("")]
    [InlineData("small")]
    [InlineData("mediumsizevalue")]
    public async Task DeleteCommand_ValuesOfDifferentSizes_ShouldWork(string value)
    {
        // Arrange
        await ConnectAsync();
        var key = $"size_test_{value.Length}";
        
        if (!string.IsNullOrEmpty(value))
        {
            await SendSetCommandAsync(key, value);
        }
        else
        {
            await SendSetCommandAsync(key, value);
        }

        // Act
        var response = await SendDeleteCommandAsync(key);

        // Assert
        response.Should().Contain("DELETED");
    }
}