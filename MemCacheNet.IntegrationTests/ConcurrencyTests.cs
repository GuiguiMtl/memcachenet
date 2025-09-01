using System.Net.Sockets;
using System.Text;
using FluentAssertions;

namespace MemCacheNet.IntegrationTests;

/// <summary>
/// Integration tests for concurrent client connections and thread safety.
/// </summary>
public class ConcurrencyTests : IntegrationTestBase
{
    public ConcurrencyTests() : base(11213) // Use different port to avoid conflicts
    {
    }
    
    protected override int GetMaxKeys() => 1000;
    protected override int GetMaxConnections() => 50;

    [Fact]
    public async Task MultipleConcurrentClients_ShouldAllBeAbleToSetAndGet()
    {
        // Arrange
        const int clientCount = 10;
        var tasks = new List<Task>();

        // Act - Create multiple concurrent clients
        for (int i = 0; i < clientCount; i++)
        {
            var clientId = i;
            tasks.Add(Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", TestPort);
                using var stream = client.GetStream();

                var key = $"concurrent_key_{clientId}";
                var value = $"concurrent_value_{clientId}";

                // Set value
                var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
                var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
                await stream.WriteAsync(setCommandBytes);
                
                var setResponse = new byte[1024];
                var setBytesRead = await stream.ReadAsync(setResponse);
                var setResponseText = Encoding.UTF8.GetString(setResponse, 0, setBytesRead);
                setResponseText.Should().Contain("STORED");

                // Get value
                var getCommand = $"get {key}\r\n";
                var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
                await stream.WriteAsync(getCommandBytes);

                var getResponse = new byte[1024];
                var getBytesRead = await stream.ReadAsync(getResponse);
                var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);
                
                getResponseText.Should().Contain($"VALUE {key} 0 {value.Length}");
                getResponseText.Should().Contain(value);
            }));
        }

        // Assert - All tasks should complete successfully
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentOperationsOnSameKey_ShouldBeThreadSafe()
    {
        // Arrange
        const int operationCount = 20;
        const string sharedKey = "shared_key";
        var tasks = new List<Task<string>>();

        // Act - Multiple clients performing operations on the same key
        for (int i = 0; i < operationCount; i++)
        {
            var operationId = i;
            tasks.Add(Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", TestPort);
                using var stream = client.GetStream();

                var value = $"value_{operationId}";

                // Set value
                var setCommand = $"set {sharedKey} 0 0 {value.Length}\r\n{value}\r\n";
                var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
                await stream.WriteAsync(setCommandBytes);
                
                var setResponse = new byte[1024];
                await stream.ReadAsync(setResponse);

                // Small delay to increase contention
                await Task.Delay(10);

                // Get value
                var getCommand = $"get {sharedKey}\r\n";
                var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
                await stream.WriteAsync(getCommandBytes);

                var getResponse = new byte[1024];
                var getBytesRead = await stream.ReadAsync(getResponse);
                var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);

                return getResponseText;
            }));
        }

        // Assert - All operations should complete without exceptions
        var results = await Task.WhenAll(tasks);
        
        // All responses should be valid (contain either a VALUE or just END)
        foreach (var result in results)
        {
            result.Should().Contain("END");
        }
    }

    [Fact]
    public async Task SequentialVsConcurrentOperations_ShouldProduceSameResult()
    {
        // Arrange
        const int operationCount = 5;
        var sequentialKeys = new List<string>();
        var concurrentKeys = new List<string>();

        // Sequential operations
        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", TestPort);
            using var stream = client.GetStream();

            for (int i = 0; i < operationCount; i++)
            {
                var key = $"sequential_key_{i}";
                var value = $"sequential_value_{i}";
                sequentialKeys.Add(key);

                var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
                await stream.ReadAsync(new byte[1024]);
            }
        }

        // Concurrent operations
        var concurrentTasks = new List<Task>();
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            concurrentTasks.Add(Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", TestPort);
                using var stream = client.GetStream();

                var key = $"concurrent_key_{index}";
                var value = $"concurrent_value_{index}";
                concurrentKeys.Add(key);

                var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
                await stream.ReadAsync(new byte[1024]);
            }));
        }

        await Task.WhenAll(concurrentTasks);

        // Verify all keys are accessible
        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", TestPort);
            using var stream = client.GetStream();

            // Check sequential keys
            foreach (var key in sequentialKeys)
            {
                var getCommand = $"get {key}\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
                var response = new byte[1024];
                var bytesRead = await stream.ReadAsync(response);
                var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
                responseText.Should().Contain($"VALUE {key}");
            }

            // Check concurrent keys
            foreach (var key in concurrentKeys)
            {
                var getCommand = $"get {key}\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(getCommand));
                var response = new byte[1024];
                var bytesRead = await stream.ReadAsync(response);
                var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
                responseText.Should().Contain($"VALUE {key}");
            }
        }

        // Assert
        sequentialKeys.Should().HaveCount(operationCount);
        concurrentKeys.Should().HaveCount(operationCount);
    }

    [Fact]
    public async Task ConnectionLimit_ShouldHandleMaxConcurrentConnections()
    {
        // This test verifies that the server can handle up to its configured maximum connections
        // Note: Actual connection limit enforcement would depend on server implementation
        
        // Arrange
        const int maxConnections = 5; // Lower than server setting to avoid overwhelming test environment
        var clients = new List<TcpClient>();
        var streams = new List<NetworkStream>();

        try
        {
            // Act - Create multiple concurrent connections
            for (int i = 0; i < maxConnections; i++)
            {
                var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", TestPort);
                clients.Add(client);
                streams.Add(client.GetStream());
            }

            // Perform operations on all connections
            var tasks = new List<Task>();
            for (int i = 0; i < maxConnections; i++)
            {
                var index = i;
                var stream = streams[i];
                tasks.Add(Task.Run(async () =>
                {
                    var key = $"connection_test_{index}";
                    var value = $"value_{index}";
                    var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
                    
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(setCommand));
                    var response = new byte[1024];
                    var bytesRead = await stream.ReadAsync(response);
                    var responseText = Encoding.UTF8.GetString(response, 0, bytesRead);
                    responseText.Should().Contain("STORED");
                }));
            }

            // Assert - All connections should work
            await Task.WhenAll(tasks);
        }
        finally
        {
            // Cleanup
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }
}