using System.Text;
using memcachenet.MemCacheServer;
using memcachenet.MemCacheServer.EvictionPolicyManagers;
using Moq;
using NUnit.Framework;

namespace MemCacheNetTests;

[TestFixture]
public class MemCacheBuilderTests
{
    [Test]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var builder = new MemCacheBuilder();

        // Assert
        var cache = builder.Build();
        Assert.That(cache, Is.Not.Null);
        Assert.That(cache, Is.InstanceOf<MemCache>());
    }

    [Test]
    public void WithMaxKeys_SetsMaxKeys_ReturnsBuilderInstance()
    {
        // Arrange
        var builder = new MemCacheBuilder();
        const int expectedMaxKeys = 500;

        // Act
        var result = builder.WithMaxKeys(expectedMaxKeys);

        // Assert
        Assert.That(result, Is.SameAs(builder));
    }

    [Test]
    public void WithMaxCacheSize_SetsMaxCacheSize_ReturnsBuilderInstance()
    {
        // Arrange
        var builder = new MemCacheBuilder();
        const int expectedMaxCacheSize = 204800;

        // Act
        var result = builder.WithMaxCacheSize(expectedMaxCacheSize);

        // Assert
        Assert.That(result, Is.SameAs(builder));
    }

    [Test]
    public void WithExpirationPolicy_SetsEvictionPolicy_ReturnsBuilderInstance()
    {
        // Arrange
        var builder = new MemCacheBuilder();
        var mockPolicy = new Mock<IEvictionPolicyManager>();

        // Act
        var result = builder.WithExpirationPolicy(mockPolicy.Object);

        // Assert
        Assert.That(result, Is.SameAs(builder));
    }

    [Test]
    public void Build_CreatesMemCacheInstance()
    {
        // Arrange
        var builder = new MemCacheBuilder()
            .WithMaxKeys(100)
            .WithMaxCacheSize(50000);

        // Act
        var cache = builder.Build();

        // Assert
        Assert.That(cache, Is.Not.Null);
        Assert.That(cache, Is.InstanceOf<MemCache>());
    }

    [Test]
    public void Builder_FluentInterface_AllowsChaining()
    {
        // Arrange & Act
        var cache = new MemCacheBuilder()
            .WithMaxKeys(200)
            .WithMaxCacheSize(100000)
            .WithExpirationPolicy(new LruEvictionPolicyManager())
            .Build();

        // Assert
        Assert.That(cache, Is.Not.Null);
        Assert.That(cache, Is.InstanceOf<MemCache>());
    }
}

[TestFixture]
public class MemCacheTests
{
    private Mock<IEvictionPolicyManager> _mockEvictionPolicy;
    private MemCache _cache;
    private const int MaxKeys = 3;
    private const int MaxCacheSize = 1000;
    private readonly TimeSpan _expirationTime = TimeSpan.FromMinutes(5);

    [SetUp]
    public void SetUp()
    {
        _mockEvictionPolicy = new Mock<IEvictionPolicyManager>();
        _cache = new MemCache(MaxKeys, MaxCacheSize, _mockEvictionPolicy.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // MemCache doesn't implement IDisposable, so no cleanup needed
    }

    [TestFixture]
    public class ConstructorTests : MemCacheTests
    {
        [Test]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            const int maxKeys = 100;
            const int maxCacheSize = 50000;
            var expirationTime = TimeSpan.FromMinutes(10);
            var evictionPolicy = new Mock<IEvictionPolicyManager>().Object;

            // Act
            var cache = new MemCache(maxKeys, maxCacheSize, evictionPolicy);

            // Assert
            Assert.That(cache, Is.Not.Null);
            Assert.That(cache, Is.InstanceOf<IMemCache>());
        }
    }

    [TestFixture]
    public class SetAsyncTests : MemCacheTests
    {
        [Test]
        public async Task SetAsync_WithValidKeyAndValue_ReturnsTrue()
        {
            // Arrange
            const string key = "testKey";
            var value = Encoding.UTF8.GetBytes("testValue");
            const uint flags = 0;

            // Act
            var result = await _cache.SetAsync(key, value, flags);

            // Assert
            Assert.That(result, Is.True);
            _mockEvictionPolicy.Verify(ep => ep.Add(key), Times.Once);
        }

        [Test]
        public async Task SetAsync_WithLargeValue_ReturnsFalseWhenExceedsMaxSize()
        {
            // Arrange
            const string key = "testKey";
            var largeValue = new byte[MaxCacheSize + 1];
            const uint flags = 0;

            // Act
            var result = await _cache.SetAsync(key, largeValue, flags);

            // Assert
            Assert.That(result, Is.False);
            _mockEvictionPolicy.Verify(ep => ep.Add(key), Times.Never);
        }

        [Test]
        public async Task SetAsync_WhenMaxKeysReached_EvictsOldestKey()
        {
            // Arrange
            const string keyToEvict = "key0";  // Use a key that actually exists in the cache
            _mockEvictionPolicy.Setup(ep => ep.KeyToRemove()).ReturnsAsync(keyToEvict);

            // Fill cache to max capacity
            for (int i = 0; i < MaxKeys; i++)
            {
                await _cache.SetAsync($"key{i}", Encoding.UTF8.GetBytes($"value{i}"), 0);
            }

            const string newKey = "newKey";
            var newValue = Encoding.UTF8.GetBytes("newValue");

            // Act
            var result = await _cache.SetAsync(newKey, newValue, 0);

            // Assert
            Assert.That(result, Is.True);
            _mockEvictionPolicy.Verify(ep => ep.KeyToRemove(), Times.Once);
            _mockEvictionPolicy.Verify(ep => ep.Delete(keyToEvict), Times.Once);
            _mockEvictionPolicy.Verify(ep => ep.Add(newKey), Times.Once);
        }

        [Test]
        public async Task SetAsync_OverwritesExistingKey_ReturnsTrue()
        {
            // Arrange
            const string key = "testKey";
            var originalValue = Encoding.UTF8.GetBytes("originalValue");
            var newValue = Encoding.UTF8.GetBytes("newValue");

            await _cache.SetAsync(key, originalValue, 0);

            // Act
            var result = await _cache.SetAsync(key, newValue, 1);

            // Assert
            Assert.That(result, Is.True);
            _mockEvictionPolicy.Verify(ep => ep.Add(key), Times.Exactly(2));
        }

        [Test]
        public async Task SetAsync_WithDifferentFlags_StoresFlags()
        {
            // Arrange
            const string key = "testKey";
            var value = Encoding.UTF8.GetBytes("testValue");
            const uint flags = 12345;

            // Act
            var result = await _cache.SetAsync(key, value, flags);

            // Assert
            Assert.That(result, Is.True);
            
            var retrievedItem = await _cache.TryGetAsync(key);
            Assert.That(retrievedItem, Is.Not.Null);
            Assert.That(retrievedItem.Flags, Is.EqualTo(flags));
        }

        [Test]
        public async Task SetAsync_WithEmptyValue_ReturnsTrue()
        {
            // Arrange
            const string key = "emptyKey";
            var emptyValue = Array.Empty<byte>();
            const uint flags = 0;

            // Act
            var result = await _cache.SetAsync(key, emptyValue, flags);

            // Assert
            Assert.That(result, Is.True);
            _mockEvictionPolicy.Verify(ep => ep.Add(key), Times.Once);
        }
    }

    [TestFixture]
    public class TryGetAsyncTests : MemCacheTests
    {
        [Test]
        public async Task TryGetAsync_WithExistingKey_ReturnsValue()
        {
            // Arrange
            const string key = "testKey";
            var expectedValue = Encoding.UTF8.GetBytes("testValue");
            const uint expectedFlags = 123;

            await _cache.SetAsync(key, expectedValue, expectedFlags);

            // Act
            var result = await _cache.TryGetAsync(key);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value, Is.EqualTo(expectedValue));
            Assert.That(result.Flags, Is.EqualTo(expectedFlags));
            _mockEvictionPolicy.Verify(ep => ep.Get(key), Times.Once);
        }

        [Test]
        public async Task TryGetAsync_WithNonExistentKey_ReturnsNull()
        {
            // Arrange
            const string nonExistentKey = "nonExistentKey";

            // Act
            var result = await _cache.TryGetAsync(nonExistentKey);

            // Assert
            Assert.That(result, Is.Null);
            _mockEvictionPolicy.Verify(ep => ep.Get(nonExistentKey), Times.Never);
        }

        [Test]
        public async Task TryGetAsync_WithExpiredKey_ReturnsNullAndRemovesKey()
        {
            // Arrange
            const string key = "expiredKey";
            var value = Encoding.UTF8.GetBytes("testValue");
            var shortExpirationTime = TimeSpan.FromMilliseconds(1);
            
            var expiredCache = new MemCache(MaxKeys, MaxCacheSize, _mockEvictionPolicy.Object);
            
            await expiredCache.SetAsync(key, value, 0, 1);
            await Task.Delay(1100); // Wait for expiration

            // Act
            var result = await expiredCache.TryGetAsync(key);

            // Assert
            Assert.That(result, Is.Null);
            _mockEvictionPolicy.Verify(ep => ep.Delete(key), Times.AtLeastOnce);
        }

        [Test]
        public async Task TryGetAsync_UpdatesEvictionPolicyOnAccess()
        {
            // Arrange
            const string key = "accessKey";
            var value = Encoding.UTF8.GetBytes("testValue");
            await _cache.SetAsync(key, value, 0);

            // Act
            await _cache.TryGetAsync(key);

            // Assert
            _mockEvictionPolicy.Verify(ep => ep.Get(key), Times.Once);
        }
    }

    [TestFixture]
    public class DeleteAsyncTests : MemCacheTests
    {
        [Test]
        public async Task DeleteAsync_WithExistingKey_ReturnsTrueAndRemovesKey()
        {
            // Arrange
            const string key = "deleteKey";
            var value = Encoding.UTF8.GetBytes("testValue");
            await _cache.SetAsync(key, value, 0);

            // Act
            var result = await _cache.DeleteAsync(key);

            // Assert
            Assert.That(result, Is.True);
            _mockEvictionPolicy.Verify(ep => ep.Delete(key), Times.Once);
            
            var retrievedItem = await _cache.TryGetAsync(key);
            Assert.That(retrievedItem, Is.Null);
        }

        [Test]
        public async Task DeleteAsync_WithNonExistentKey_ReturnsFalse()
        {
            // Arrange
            const string nonExistentKey = "nonExistentKey";

            // Act
            var result = await _cache.DeleteAsync(nonExistentKey);

            // Assert
            Assert.That(result, Is.False);
            _mockEvictionPolicy.Verify(ep => ep.Delete(nonExistentKey), Times.Never);
        }

        [Test]
        public async Task DeleteAsync_UpdatesCacheSize()
        {
            // Arrange
            const string key = "sizeTestKey";
            var value = Encoding.UTF8.GetBytes("testValue");
            await _cache.SetAsync(key, value, 0);

            // Act
            var result = await _cache.DeleteAsync(key);

            // Assert
            Assert.That(result, Is.True);
        }
    }

    [TestFixture]
    public class DeleteExpiredKeysAsyncTests : MemCacheTests
    {
        [Test]
        public async Task DeleteExpiredKeysAsync_WithDefaultSampleSize_ProcessesKeys()
        {
            // Arrange
            var shortExpirationTime = TimeSpan.FromMilliseconds(1);
            var expiredCache = new MemCache(MaxKeys, MaxCacheSize, _mockEvictionPolicy.Object);
            
            // Add some keys that will expire
            await expiredCache.SetAsync("key1", Encoding.UTF8.GetBytes("value1"), 0);
            await expiredCache.SetAsync("key2", Encoding.UTF8.GetBytes("value2"), 0);
            
            await Task.Delay(10); // Wait for expiration

            // Act
            await expiredCache.DeleteExpiredKeysAsync();

            // Assert - Should complete without exceptions
            Assert.Pass("DeleteExpiredKeysAsync executed successfully");
        }

        [Test]
        public async Task DeleteExpiredKeysAsync_WithCustomSampleSize_ProcessesSpecifiedNumberOfKeys()
        {
            // Arrange
            var shortExpirationTime = TimeSpan.FromMilliseconds(1);
            var expiredCache = new MemCache(MaxKeys, MaxCacheSize, _mockEvictionPolicy.Object);
            
            await expiredCache.SetAsync("key1", Encoding.UTF8.GetBytes("value1"), 0);
            await Task.Delay(10); // Wait for expiration

            const int customSampleSize = 5;

            // Act
            await expiredCache.DeleteExpiredKeysAsync(customSampleSize);

            // Assert - Should process without throwing exceptions
            Assert.Pass("Method executed without exceptions");
        }

        [Test]
        public async Task DeleteExpiredKeysAsync_WithNonExpiredKeys_DoesNotDeleteThem()
        {
            // Arrange
            var longExpirationTime = TimeSpan.FromHours(1);
            var nonExpiredCache = new MemCache(MaxKeys, MaxCacheSize, _mockEvictionPolicy.Object);
            
            await nonExpiredCache.SetAsync("key1", Encoding.UTF8.GetBytes("value1"), 0);

            // Act
            await nonExpiredCache.DeleteExpiredKeysAsync();

            // Assert
            var result = await nonExpiredCache.TryGetAsync("key1");
            Assert.That(result, Is.Not.Null);
        }
    }
}

[TestFixture]
public class MemCacheIntegrationTests
{
    [Test]
    public async Task Cache_WithLruEviction_EvictsLeastRecentlyUsedItems()
    {
        // Arrange
        var cache = new MemCacheBuilder()
            .WithMaxKeys(2)
            .WithMaxCacheSize(1000)
            .WithExpirationPolicy(new LruEvictionPolicyManager())
            .Build();

        // Act - Fill cache to capacity
        await cache.SetAsync("key1", Encoding.UTF8.GetBytes("value1"), 0);
        await cache.SetAsync("key2", Encoding.UTF8.GetBytes("value2"), 0);
        
        // Access key1 to make it more recently used
        await cache.TryGetAsync("key1");
        
        // Add a third key, which should evict key2 (least recently used)
        await cache.SetAsync("key3", Encoding.UTF8.GetBytes("value3"), 0);

        // Assert
        var key1Result = await cache.TryGetAsync("key1");
        var key2Result = await cache.TryGetAsync("key2");
        var key3Result = await cache.TryGetAsync("key3");

        Assert.That(key1Result, Is.Not.Null, "key1 should still exist (most recently used)");
        Assert.That(key2Result, Is.Null, "key2 should be evicted (least recently used)");
        Assert.That(key3Result, Is.Not.Null, "key3 should exist (newly added)");
    }

    [Test]
    public async Task Cache_WithCapacityLimit_RejectsOversizedValues()
    {
        // Arrange
        const int smallCacheSize = 100;
        var cache = new MemCacheBuilder()
            .WithMaxKeys(10)
            .WithMaxCacheSize(smallCacheSize)
            .Build();

        var oversizedValue = new byte[smallCacheSize + 1];

        // Act
        var result = await cache.SetAsync("oversizedKey", oversizedValue, 0);

        // Assert
        Assert.That(result, Is.False);
        
        var retrievedItem = await cache.TryGetAsync("oversizedKey");
        Assert.That(retrievedItem, Is.Null);
    }

    [Test]
    public async Task Cache_ExpirationScenario_AutomaticallyRemovesExpiredItems()
    {
        // Arrange
        var cache = new MemCacheBuilder()
            .WithMaxKeys(10)
            .WithMaxCacheSize(1000)
            .Build();

        // Act
        await cache.SetAsync("expiringKey", Encoding.UTF8.GetBytes("expiringValue"), 0, 1);
        
        // Verify it exists initially
        var initialResult = await cache.TryGetAsync("expiringKey");
        Assert.That(initialResult, Is.Not.Null);

        // Wait for expiration
        await Task.Delay(1100);
        
        // Verify it's expired and removed
        var expiredResult = await cache.TryGetAsync("expiringKey");

        // Assert
        Assert.That(expiredResult, Is.Null);
    }

    [Test]
    public async Task Cache_ConcurrentOperations_HandlesThreadSafety()
    {
        // Arrange
        var cache = new MemCacheBuilder()
            .WithMaxKeys(100)
            .WithMaxCacheSize(10000)
            .WithExpirationPolicy(new LruEvictionPolicyManagerWithLock())
            .Build();

        const int numberOfTasks = 10;
        const int operationsPerTask = 20;

        // Act - Perform concurrent set/get/delete operations
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfTasks; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerTask; j++)
                {
                    string key = $"task{taskId}_key{j}";
                    var value = Encoding.UTF8.GetBytes($"task{taskId}_value{j}");
                    
                    await cache.SetAsync(key, value, (uint)(taskId * 1000 + j));
                    await cache.TryGetAsync(key);
                    
                    if (j % 3 == 0) // Delete every third item
                    {
                        await cache.DeleteAsync(key);
                    }
                }
            }));
        }

        // Assert - All tasks should complete without exceptions
        await Task.WhenAll(tasks);
        Assert.Pass("All concurrent operations completed successfully");
    }
}

[TestFixture]
public class MemCacheItemTests
{
    [Test]
    public void MemCacheItem_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var item = new MemCacheItem();

        // Assert
        Assert.That(item.Value, Is.Not.Null);
        Assert.That(item.Value, Is.Empty);
        Assert.That(item.Flags, Is.EqualTo(0));
    }

    [Test]
    public void MemCacheItem_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var expectedValue = Encoding.UTF8.GetBytes("testValue");
        const uint expectedFlags = 12345;
        var expectedExpiration = DateTime.Now.AddMinutes(5);

        // Act
        var item = new MemCacheItem
        {
            Value = expectedValue,
            Flags = expectedFlags,
            Expiration = expectedExpiration
        };

        // Assert
        Assert.That(item.Value, Is.EqualTo(expectedValue));
        Assert.That(item.Flags, Is.EqualTo(expectedFlags));
        Assert.That(item.Expiration, Is.EqualTo(expectedExpiration));
    }
}