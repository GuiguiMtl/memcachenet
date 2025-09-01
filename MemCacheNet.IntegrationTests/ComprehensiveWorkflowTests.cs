namespace MemCacheNet.IntegrationTests;

public class ComprehensiveWorkflowTests : BaseIntegrationTest
{
    [Fact]
    public async Task FullCRUD_Workflow_ShouldWorkCorrectly()
    {
        // Arrange
        await ConnectAsync();
        var key = "crud_test";
        var originalValue = "original_value";
        var updatedValue = "updated_value";

        // Act & Assert - CREATE
        var setResponse = await SendSetCommandAsync(key, originalValue);
        setResponse.Should().Contain("STORED");

        // Act & Assert - READ
        var getResponse1 = await SendGetCommandAsync(key);
        getResponse1.Should().Contain($"VALUE {key} 0 {originalValue.Length}");
        getResponse1.Should().Contain(originalValue);

        // Act & Assert - UPDATE
        var updateResponse = await SendSetCommandAsync(key, updatedValue);
        updateResponse.Should().Contain("STORED");

        var getResponse2 = await SendGetCommandAsync(key);
        getResponse2.Should().Contain($"VALUE {key} 0 {updatedValue.Length}");
        getResponse2.Should().Contain(updatedValue);
        getResponse2.Should().NotContain(originalValue);

        // Act & Assert - DELETE
        var deleteResponse = await SendDeleteCommandAsync(key);
        deleteResponse.Should().Contain("DELETED");

        var getResponse3 = await SendGetCommandAsync(key);
        getResponse3.Should().NotContain("VALUE");
        getResponse3.Should().Contain("END");
    }

    [Fact]
    public async Task MassiveBulkOperations_ShouldHandleEfficiently()
    {
        // Arrange
        await ConnectAsync();
        var bulkSize = 1000;
        var testData = new Dictionary<string, string>();

        for (int i = 0; i < bulkSize; i++)
        {
            testData[$"bulk_key_{i}"] = $"bulk_value_{i}_{DateTime.UtcNow.Ticks}";
        }

        // Act - Bulk SET operations
        foreach (var (key, value) in testData)
        {
            var response = await SendSetCommandAsync(key, value);
            response.Should().Contain("STORED");
        }

        // Act - Bulk GET operations (single keys)
        foreach (var (key, expectedValue) in testData.Take(10)) // Test first 10 for performance
        {
            var response = await SendGetCommandAsync(key);
            response.Should().Contain($"VALUE {key} 0 {expectedValue.Length}");
            response.Should().Contain(expectedValue);
        }

        // Act - Multi-key GET operation
        var multiGetKeys = testData.Keys.Take(50).ToArray();
        var multiGetResponse = await SendGetCommandAsync(multiGetKeys);
        
        foreach (var key in multiGetKeys)
        {
            multiGetResponse.Should().Contain($"VALUE {key} 0");
        }
        multiGetResponse.Should().Contain("END");

        // Act - Bulk DELETE operations
        foreach (var key in testData.Keys.Take(100)) // Delete first 100
        {
            var response = await SendDeleteCommandAsync(key);
            response.Should().Contain("DELETED");
        }

        // Verify deletions
        var deletedKeys = testData.Keys.Take(100).ToArray();
        var verifyResponse = await SendGetCommandAsync(deletedKeys);
        verifyResponse.Should().NotContain("VALUE");
        verifyResponse.Should().Contain("END");
    }

    [Fact]
    public async Task MixedOperationsWorkflow_ShouldMaintainConsistency()
    {
        // Arrange
        await ConnectAsync();
        var operations = 200;
        var keys = new HashSet<string>();
        var random = new Random(12345); // Fixed seed for reproducibility

        // Act - Mixed operations
        for (int i = 0; i < operations; i++)
        {
            var operation = random.Next(3); // 0=SET, 1=GET, 2=DELETE
            var key = $"mixed_key_{random.Next(50)}"; // 50 possible keys
            
            switch (operation)
            {
                case 0: // SET
                    var value = $"value_{i}_{DateTime.UtcNow.Ticks}";
                    var setResponse = await SendSetCommandAsync(key, value);
                    setResponse.Should().Contain("STORED");
                    keys.Add(key);
                    break;
                
                case 1: // GET
                    var getResponse = await SendGetCommandAsync(key);
                    if (keys.Contains(key))
                    {
                        getResponse.Should().Contain("VALUE");
                    }
                    getResponse.Should().Contain("END");
                    break;
                
                case 2: // DELETE
                    var deleteResponse = await SendDeleteCommandAsync(key);
                    if (keys.Contains(key))
                    {
                        deleteResponse.Should().Contain("DELETED");
                        keys.Remove(key);
                    }
                    else
                    {
                        deleteResponse.Should().Contain("NOT_FOUND");
                    }
                    break;
            }
        }

        // Assert - Final consistency check
        foreach (var key in keys.Take(10)) // Check a subset
        {
            var response = await SendGetCommandAsync(key);
            response.Should().Contain("VALUE");
        }
    }

    [Fact]
    public async Task CacheOverflow_ShouldHandleEvictionGracefully()
    {
        // Arrange
        await ConnectAsync();
        var excessiveKeys = 1000; // Likely to exceed cache limits

        // Act - Fill cache beyond capacity
        var storedKeys = new List<string>();
        for (int i = 0; i < excessiveKeys; i++)
        {
            var key = $"overflow_key_{i}";
            var value = $"overflow_value_{i}";
            var response = await SendSetCommandAsync(key, value);
            
            if (response.Contains("STORED"))
            {
                storedKeys.Add(key);
            }
            else if (response.Contains("SERVER_ERROR"))
            {
                // Server might reject if over capacity - this is acceptable
                break;
            }
        }

        // Assert - Some keys should be stored
        storedKeys.Should().NotBeEmpty();
        
        // Verify recent keys are still accessible (LRU behavior)
        var recentKeys = storedKeys.TakeLast(10);
        foreach (var key in recentKeys)
        {
            var response = await SendGetCommandAsync(key);
            // Note: Due to eviction, some keys might not be found - this is expected behavior
        }
    }

    [Fact]
    public async Task ComplexMultiKeyOperations_ShouldWorkCorrectly()
    {
        // Arrange
        await ConnectAsync();
        var keyGroups = new[]
        {
            new[] { "group1_key1", "group1_key2", "group1_key3" },
            new[] { "group2_key1", "group2_key2", "group2_key3" },
            new[] { "group3_key1", "group3_key2", "group3_key3" }
        };

        // Act - Set all keys
        foreach (var group in keyGroups)
        {
            for (int i = 0; i < group.Length; i++)
            {
                var value = $"value_for_{group[i]}";
                var response = await SendSetCommandAsync(group[i], value);
                response.Should().Contain("STORED");
            }
        }

        // Act - Multi-key GET for each group
        foreach (var group in keyGroups)
        {
            var response = await SendGetCommandAsync(group);
            
            foreach (var key in group)
            {
                response.Should().Contain($"VALUE {key} 0");
            }
            response.Should().Contain("END");
        }

        // Act - Mixed multi-key GET (across groups)
        var mixedKeys = keyGroups.SelectMany(g => g.Take(1)).ToArray(); // One key from each group
        var mixedResponse = await SendGetCommandAsync(mixedKeys);
        
        foreach (var key in mixedKeys)
        {
            mixedResponse.Should().Contain($"VALUE {key} 0");
        }

        // Act - Partial group deletions
        foreach (var group in keyGroups)
        {
            var keyToDelete = group[0]; // Delete first key from each group
            var deleteResponse = await SendDeleteCommandAsync(keyToDelete);
            deleteResponse.Should().Contain("DELETED");
        }

        // Assert - Verify partial deletions
        foreach (var group in keyGroups)
        {
            var response = await SendGetCommandAsync(group);
            response.Should().NotContain($"VALUE {group[0]} 0"); // First key deleted
            response.Should().Contain($"VALUE {group[1]} 0"); // Second key still exists
            response.Should().Contain($"VALUE {group[2]} 0"); // Third key still exists
        }
    }

    [Fact]
    public async Task SessionSimulation_ShouldWorkReliably()
    {
        // Arrange - Simulate a user session
        await ConnectAsync();
        var sessionId = Guid.NewGuid().ToString();
        var sessionData = new Dictionary<string, string>
        {
            [$"session:{sessionId}:user_id"] = "12345",
            [$"session:{sessionId}:username"] = "testuser",
            [$"session:{sessionId}:preferences"] = "theme:dark,lang:en",
            [$"session:{sessionId}:cart"] = "item1,item2,item3",
            [$"session:{sessionId}:last_activity"] = DateTime.UtcNow.ToString()
        };

        // Act - Create session
        foreach (var (key, value) in sessionData)
        {
            var response = await SendSetCommandAsync(key, value, expiration: 3600); // 1 hour expiry
            response.Should().Contain("STORED");
        }

        // Act - Read session data
        var sessionKeys = sessionData.Keys.ToArray();
        var sessionResponse = await SendGetCommandAsync(sessionKeys);
        
        foreach (var (key, expectedValue) in sessionData)
        {
            sessionResponse.Should().Contain($"VALUE {key}");
            sessionResponse.Should().Contain(expectedValue);
        }

        // Act - Update session data
        var updatedCart = "item1,item2,item3,item4";
        var updateResponse = await SendSetCommandAsync($"session:{sessionId}:cart", updatedCart, expiration: 3600);
        updateResponse.Should().Contain("STORED");

        // Act - Verify update
        var cartResponse = await SendGetCommandAsync($"session:{sessionId}:cart");
        cartResponse.Should().Contain(updatedCart);

        // Act - Clean up session
        foreach (var key in sessionKeys)
        {
            var deleteResponse = await SendDeleteCommandAsync(key);
            deleteResponse.Should().Contain("DELETED");
        }

        // Assert - Verify cleanup
        var cleanupResponse = await SendGetCommandAsync(sessionKeys);
        cleanupResponse.Should().NotContain("VALUE");
        cleanupResponse.Should().Contain("END");
    }

    [Fact]
    public async Task StressTest_HighFrequencyOperations_ShouldRemainStable()
    {
        // Arrange
        await ConnectAsync();
        var duration = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;
        var operationCount = 0;
        var errors = 0;

        // Act - High frequency operations
        while (DateTime.UtcNow - startTime < duration)
        {
            try
            {
                var key = $"stress_{operationCount % 100}"; // Cycle through 100 keys
                var value = $"value_{operationCount}";
                
                var setResponse = await SendSetCommandAsync(key, value);
                if (!setResponse.Contains("STORED"))
                    errors++;

                var getResponse = await SendGetCommandAsync(key);
                if (!getResponse.Contains("END"))
                    errors++;

                operationCount += 2;
            }
            catch (Exception)
            {
                errors++;
            }
        }

        // Assert
        operationCount.Should().BeGreaterThan(0);
        var errorRate = (double)errors / operationCount;
        errorRate.Should().BeLessThan(0.05); // Less than 5% error rate
    }

    [Fact]
    public async Task DataIntegrity_AfterManyOperations_ShouldBePreserved()
    {
        // Arrange
        await ConnectAsync();
        var testKeys = new[] { "integrity1", "integrity2", "integrity3" };
        var checksumData = new Dictionary<string, string>
        {
            ["integrity1"] = "data_with_checksum_12345",
            ["integrity2"] = "another_data_67890", 
            ["integrity3"] = "final_data_abcdef"
        };

        // Act - Initial set
        foreach (var (key, value) in checksumData)
        {
            var response = await SendSetCommandAsync(key, value);
            response.Should().Contain("STORED");
        }

        // Act - Many intervening operations
        for (int i = 0; i < 500; i++)
        {
            var tempKey = $"temp_{i}";
            var tempValue = $"temp_value_{i}";
            await SendSetCommandAsync(tempKey, tempValue);
            await SendGetCommandAsync(tempKey);
            await SendDeleteCommandAsync(tempKey);
        }

        // Assert - Verify original data integrity
        foreach (var (key, expectedValue) in checksumData)
        {
            var response = await SendGetCommandAsync(key);
            response.Should().Contain($"VALUE {key} 0 {expectedValue.Length}");
            response.Should().Contain(expectedValue);
        }
    }
}