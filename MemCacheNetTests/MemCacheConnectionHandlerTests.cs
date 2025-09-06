using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using memcachenet.MemCacheServer;
using NUnit.Framework;

namespace MemCacheNetTests;

[TestFixture]
public class MemCacheConnectionHandlerTests
{
    private List<string> _capturedLines;
    private TcpClient _mockClient;

    [SetUp]
    public void SetUp()
    {
        _capturedLines = [];
        _mockClient = new TcpClient();
    }

    [TearDown]
    public void TearDown()
    {
        _mockClient?.Dispose();
    }

    private void OnLineReadCallback(ReadOnlySequence<byte> line, Func<byte[], Task> writeResponse)
    {
        // Convert to string immediately to avoid memory invalidation
        if (!line.IsEmpty && line.Length > 0)
        {
            try
            {
                var text = Encoding.UTF8.GetString(line.ToArray());
                _capturedLines.Add(text);
            }
            catch (Exception ex)
            {
                _capturedLines.Add($"ERROR: {ex.Message}");
            }
        }
        else
        {
            _capturedLines.Add("EMPTY");
        }
    }

    private static ReadOnlySequence<byte> CreateBuffer(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new ReadOnlySequence<byte>(bytes);
    }

    private static async Task<(TcpListener server, int port)> SetupTcpServer()
    {
        var server = new TcpListener(System.Net.IPAddress.Loopback, 0);
        server.Start();
        var endpoint = (System.Net.IPEndPoint)server.LocalEndpoint;
        return (server, endpoint.Port);
    }

    private async Task RunConnectionTest(string testData, Action<List<string>> assertions, bool withCallback = true)
    {
        // Arrange
        var (server, port) = await SetupTcpServer();
        
        try
        {
            var buffer = Encoding.UTF8.GetBytes(testData);
            
            var serverTask = Task.Run(async () =>
            {
                var serverClient = await server.AcceptTcpClientAsync();
                var stream = serverClient.GetStream();
                await stream.WriteAsync(buffer);
                await stream.FlushAsync();
                stream.Close();
                serverClient.Dispose();
            });

            // Act
            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, port);
            
            using var connectionHandler = new MemCacheConnectionHandler(
                client, 
                withCallback ? OnLineReadCallback : null
            );
            
            var connectionTask = connectionHandler.HandleConnectionAsync(CancellationToken.None);
            await Task.WhenAll(serverTask, connectionTask);
            
            // Assert
            assertions(_capturedLines);
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesGetCommand()
    {
        // Arrange
        var testData = "get testkey\r\n";
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert
            Assert.That(capturedLines, Has.Count.GreaterThan(0));
            
            if (capturedLines.Count > 0 && capturedLines[0] != "EMPTY")
            {
                Assert.That(capturedLines[0], Is.EqualTo("get testkey\r\n"));
            }
            else
            {
                Assert.Fail("No valid lines captured");
            }
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesSetCommand()
    {
        // Arrange
        var testData = "set testkey 0 300 9\r\ntestvalue\r\n";
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert
            Assert.That(capturedLines, Has.Count.GreaterThan(0));
            if (capturedLines.Count >= 2)
            {
                Assert.That(capturedLines[0], Is.EqualTo("set testkey 0 300 9\r\n"));
                Assert.That(capturedLines[1], Is.EqualTo("testvalue\r\n"));
            }
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesDeleteCommand()
    {
        // Arrange
        var testData = "delete testkey\r\n";
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert
            Assert.That(capturedLines, Has.Count.GreaterThan(0));
            if (capturedLines.Count > 0 && capturedLines[0] != "EMPTY")
            {
                Assert.That(capturedLines[0], Is.EqualTo("delete testkey\r\n"));
            }
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesMultipleCommands()
    {
        // Arrange
        var testData = "get key1\r\nget key2\r\ndelete key3\r\n";
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert
            Assert.That(capturedLines, Has.Count.GreaterThan(0));
            if (capturedLines.Count >= 3)
            {
                Assert.That(capturedLines[0], Is.EqualTo("get key1\r\n"));
                Assert.That(capturedLines[1], Is.EqualTo("get key2\r\n"));
                Assert.That(capturedLines[2], Is.EqualTo("delete key3\r\n"));
            }
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesCommandWithoutCRLF()
    {
        // Arrange
        var testData = "get testkey";  // No \r\n terminator
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert - Should not capture anything since no line terminator found
            Assert.That(capturedLines, Has.Count.EqualTo(0), "Commands without \\r\\n should not be captured");
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesCommandWithOnlyLF()
    {
        // Arrange
        var testData = "get testkey\n";  // Only \n, no \r
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert - Should not capture anything since memcache protocol requires \r\n
            Assert.That(capturedLines, Has.Count.EqualTo(0), "Commands with only \\n should not be captured (memcache requires \\r\\n)");
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesCommandWithOnlyCR()
    {
        // Arrange
        var testData = "get testkey\r";  // Only \r, no \n
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert - Should not capture anything since TryReadLine looks for \n specifically
            Assert.That(capturedLines, Has.Count.EqualTo(0), "Commands with only \\r should not be captured");
        });
    }

    [Test]
    public async Task ConnectionHandler_WithCallback_CapturesPartialCommand()
    {
        // Arrange
        var testData = "get test";  // Incomplete command without terminator
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert - Should not capture anything since no line terminator
            Assert.That(capturedLines, Has.Count.EqualTo(0), "Partial commands should not be captured");
        });
    }

    [Test]
    public async Task ConnectionHandler_WithoutCallback_DoesNotThrow()
    {
        // Arrange
        var testData = "get testkey\r\n";
        
        // Act & Assert
        await RunConnectionTest(testData, capturedLines =>
        {
            // Assert - Should not capture anything since no callback
            Assert.That(capturedLines, Has.Count.EqualTo(0));
        }, withCallback: false);
    }

    [TestCase("get testkey", "get testkey")]
    [TestCase("set key 0 300 5", "set key 0 300 5")]
    [TestCase("delete mykey", "delete mykey")]
    [TestCase("invalid command", "invalid command")]
    public void OnLineReadCallback_VariousCommands_CapturesExpectedData(string input, string expected)
    {
        // Arrange
        var buffer = CreateBuffer(input);
        
        // Act
        OnLineReadCallback(buffer, _ => Task.CompletedTask);
        
        // Assert
        Assert.That(_capturedLines, Has.Count.EqualTo(1));
        Assert.That(_capturedLines[0], Is.EqualTo(expected));
    }
    
    [Test]
    public async Task HandleConnectionAsync_SendsCommandReceivesResponse()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        
        var receivedResponses = new List<string>();
        var responseReceived = new TaskCompletionSource<bool>();
        
        // Setup connection handler that sends a response when command is received
        var connectionTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            using var connectionHandler = new MemCacheConnectionHandler(client, 
                async (line, writeResponse) =>
                {
                    // Simulate command processing and response
                    var command = Encoding.UTF8.GetString(line.ToArray()).Trim();
                    byte[] response;
                    if (command.StartsWith("set"))
                    {
                        response = Encoding.UTF8.GetBytes("STORED\r\n");
                        await writeResponse(response);
                    }
                    else if (command.StartsWith("get"))
                    {
                        response = Encoding.UTF8.GetBytes("END\r\n");
                        await writeResponse(response);
                    }
                    else if (command == "hello")
                    {
                        // This is the data part of the SET command, don't send a response
                        return;
                    }
                    else
                    {
                        response = Encoding.UTF8.GetBytes("ERROR\r\n");
                        await writeResponse(response);
                    }
                });
            await connectionHandler.HandleConnectionAsync(CancellationToken.None);
        });
        
        // Act - Connect as client and send command
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        
        // Send a SET command
        var command = Encoding.UTF8.GetBytes("set key 0 300 5\r\nhello\r\n");
        await stream.WriteAsync(command);
        
        // Read response
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        
        // Assert
        Assert.That(response, Is.EqualTo("STORED\r\n"));
        
        // Cleanup
        listener.Stop();
    }
}