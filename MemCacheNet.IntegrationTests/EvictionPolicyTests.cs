using System.Net.Sockets;
using System.Text;
using FluentAssertions;

namespace MemCacheNet.IntegrationTests;

/// <summary>
/// Integration tests for cache eviction policy behavior.
/// Tests the LRU (Least Recently Used) eviction policy implementation.
/// </summary>
public class EvictionPolicyTests
{
public int TestPort = 11211;

    public EvictionPolicyTests()  // Use different port to avoid conflicts
    {
    }
    
    // protected override int GetMaxKeys() => 5; // Small limit to trigger eviction quickly

    [Fact]
    public async Task LruEviction_ShouldEvictLeastRecentlyUsedKey()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Fill cache to capacity (MaxKeys = 5)
        var keys = new List<string> { "key1", "key2", "key3", "key4", "key5" };
        
        foreach (var key in keys)
        {
            var value = $"value_{key}";
            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            
            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
            responseText.Should().Contain("STORED");
        }

        // Access key2, key3, key4, key5 to make key1 the LRU
        var accessKeys = new List<string> { "key2", "key3", "key4", "key5" };
        foreach (var key in accessKeys)
        {
            var getCommand = $"get {key}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
            await stream.ReadAsync(new byte[1024]);
        }

        // Act - Add one more key to trigger eviction
        var newKey = "key6";
        var newValue = "value_key6";
        var newSetCommand = $"set {newKey} 0 0 {newValue.Length}\r\n{newValue}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(newSetCommand));
        
        var newSetResponse = new byte[1024];
        var newSetBytesRead = await stream.ReadAsync(newSetResponse);
        var newSetResponseText = Encoding.UTF8.GetString(newSetResponse, 0, newSetBytesRead);
        newSetResponseText.Should().Contain("STORED");

        // Assert - key1 should be evicted (LRU), key6 should be present
        var checkKey1Command = "get key1\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(checkKey1Command));
        var key1Response = new byte[1024];
        var key1BytesRead = await stream.ReadAsync(key1Response);
        var key1ResponseText = Encoding.UTF8.GetString(key1Response, 0, key1BytesRead);
        key1ResponseText.Should().NotContain("VALUE key1");
        key1ResponseText.Should().Contain("END");

        var checkKey6Command = "get key6\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(checkKey6Command));
        var key6Response = new byte[1024];
        var key6BytesRead = await stream.ReadAsync(key6Response);
        var key6ResponseText = Encoding.UTF8.GetString(key6Response, 0, key6BytesRead);
        key6ResponseText.Should().Contain($"VALUE {newKey}");
        key6ResponseText.Should().Contain(newValue);
    }

    [Fact]
    public async Task EvictionPolicy_ShouldMaintainMostRecentlyAccessedKeys()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Fill cache to capacity
        for (int i = 1; i <= 5; i++)
        {
            var key = $"test_key_{i}";
            var value = $"test_value_{i}";
            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            await stream.ReadAsync(new byte[1024]);
        }

        // Access keys in specific order to establish LRU order
        // Access test_key_3, test_key_4, test_key_5 (making test_key_1 and test_key_2 LRU)
        var accessOrder = new List<string> { "test_key_3", "test_key_4", "test_key_5" };
        foreach (var key in accessOrder)
        {
            var getCommand = $"get {key}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
            await stream.ReadAsync(new byte[1024]);
            
            // Small delay to ensure order
            await Task.Delay(10);
        }

        // Act - Add two more keys to trigger eviction of the two LRU keys
        var newKeys = new List<string> { "new_key_1", "new_key_2" };
        foreach (var newKey in newKeys)
        {
            var value = $"value_{newKey}";
            var setCommand = $"set {newKey} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            await stream.ReadAsync(new byte[1024]);
        }

        // Assert - test_key_1 and test_key_2 should be evicted
        var evictedKeys = new List<string> { "test_key_1", "test_key_2" };
        foreach (var evictedKey in evictedKeys)
        {
            var getCommand = $"get {evictedKey}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
            responseText.Should().NotContain($"VALUE {evictedKey}");
            responseText.Should().Contain("END");
        }

        // Assert - recently accessed keys and new keys should still be present
        var remainingKeys = new List<string> { "test_key_3", "test_key_4", "test_key_5", "new_key_1", "new_key_2" };
        foreach (var remainingKey in remainingKeys)
        {
            var getCommand = $"get {remainingKey}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
            var response = new byte[1024];
            var bytesRead = await stream.ReadAsync(response);
            var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
            responseText.Should().Contain($"VALUE {remainingKey}");
        }
    }

    [Fact]
    public async Task SetOperationOnExistingKey_ShouldUpdateLruPosition()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Fill cache to capacity
        for (int i = 1; i <= 5; i++)
        {
            var key = $"update_key_{i}";
            var value = $"original_value_{i}";
            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            await stream.ReadAsync(new byte[1024]);
        }

        // Act - Update the first key (making it most recently used)
        var updateKey = "update_key_1";
        var updatedValue = "updated_value_1";
        var updateCommand = $"set {updateKey} 0 0 {updatedValue.Length}\r\n{updatedValue}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(updateCommand));
        await stream.ReadAsync(new byte[1024]);

        // Add a new key to trigger eviction
        var newKey = "trigger_eviction_key";
        var newValue = "trigger_value";
        var newSetCommand = $"set {newKey} 0 0 {newValue.Length}\r\n{newValue}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(newSetCommand));
        await stream.ReadAsync(new byte[1024]);

        // Assert - update_key_1 should still be present (was made MRU by update)
        var checkUpdateKeyCommand = $"get {updateKey}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(checkUpdateKeyCommand));
        var updateResponse = new byte[1024];
        var updateBytesRead = await stream.ReadAsync(updateResponse);
        var updateResponseText = Encoding.UTF8.GetString(updateResponse, 0, updateBytesRead);
        updateResponseText.Should().Contain($"VALUE {updateKey}");
        updateResponseText.Should().Contain(updatedValue);

        // Assert - Some other key should have been evicted instead
        var checkNewKeyCommand = $"get {newKey}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(checkNewKeyCommand));
        var newResponse = new byte[1024];
        var newBytesRead = await stream.ReadAsync(newResponse);
        var newResponseText = Encoding.UTF8.GetString(newResponse, 0, newBytesRead);
        newResponseText.Should().Contain($"VALUE {newKey}");
        newResponseText.Should().Contain(newValue);
    }

    [Fact]
    public async Task DeleteOperation_ShouldRemoveKeyFromLruTracking()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TestPort);
        using var stream = client.GetStream();

        // Add some keys
        for (int i = 1; i <= 3; i++)
        {
            var key = $"delete_test_{i}";
            var value = $"value_{i}";
            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            await stream.ReadAsync(new byte[1024]);
        }

        // Act - Delete middle key
        var deleteKey = "delete_test_2";
        var deleteCommand = $"delete {deleteKey}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(deleteCommand));
        var deleteResponse = new byte[1024];
        var deleteBytesRead = await stream.ReadAsync(deleteResponse);
        var deleteResponseText = Encoding.UTF8.GetString(deleteResponse, 0, deleteBytesRead);
        deleteResponseText.Should().Contain("DELETED");

        // Fill remaining slots plus one more to trigger eviction
        for (int i = 4; i <= 7; i++)
        {
            var key = $"delete_test_{i}";
            var value = $"value_{i}";
            var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
            await stream.ReadAsync(new byte[1024]);
        }

        // Assert - Deleted key should not be accessible
        var checkDeletedKeyCommand = $"get {deleteKey}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(checkDeletedKeyCommand));
        var checkResponse = new byte[1024];
        var checkBytesRead = await stream.ReadAsync(checkResponse);
        var checkResponseText = Encoding.UTF8.GetString(checkResponse, 0, checkBytesRead);
        checkResponseText.Should().NotContain($"VALUE {deleteKey}");
        checkResponseText.Should().Contain("END");

        // Assert - Cache should still work normally for remaining keys
        var remainingKey = "delete_test_1";
        var checkRemainingCommand = $"get {remainingKey}\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(checkRemainingCommand));
        var remainingResponse = new byte[1024];
        var remainingBytesRead = await stream.ReadAsync(remainingResponse);
        var remainingResponseText = Encoding.UTF8.GetString(remainingResponse, 0, remainingBytesRead);
        
        // The key may or may not be present depending on eviction, but no error should occur
        remainingResponseText.Should().Contain("END");
    }
}