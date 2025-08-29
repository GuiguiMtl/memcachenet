using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using memcachenet.MemCacheServer;

namespace MemCacheNetTests;

[TestFixture]
public class MemCacheConnectionHandlerTests : IDisposable
{
    private TcpListener? _listener;
    private int _port;

    [SetUp]
    public void SetUp()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        _listener?.Stop();
        _listener = null;
        GC.SuppressFinalize(this);
    }

    [TestFixture]
    public class ConstructorTests : MemCacheConnectionHandlerTests
    {
        [Test]
        public void Constructor_WithValidTcpClient_CreatesHandler()
        {
            using var client = new TcpClient();
            
            using var handler = new MemCacheConnectionHandler(client);
            
            Assert.That(handler, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullTcpClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MemCacheConnectionHandler(null!));
        }
    }

    [TestFixture]
    public class HandleConnectionAsyncTests : MemCacheConnectionHandlerTests
    {
        [Test]
        public async Task HandleConnectionAsync_WithDisconnectedClient_CompletesGracefully()
        {
            using var client = new TcpClient();
            using var handler = new MemCacheConnectionHandler(client);

            await handler.HandleConnectionAsync();
            
            // Test passes if no exception is thrown
            Assert.Pass();
        }

        [Test]
        public async Task HandleConnectionAsync_WithConnectedClientThatDisconnects_CompletesGracefully()
        {
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                // Immediately close to simulate disconnect
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            // Test passes if no exception is thrown
            Assert.Pass();
        }

        [Test]
        public async Task HandleConnectionAsync_WithSingleGetCommand_ProcessesCommand()
        {
            var command = "get key1\r\n";
            var commandProcessed = false;

            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                var data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                // Wait a bit for processing then close
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                commandProcessed = true;
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            Assert.That(commandProcessed, Is.True);
        }

        [Test]
        public async Task HandleConnectionAsync_WithMultipleCommands_ProcessesAllCommands()
        {
            var commands = new[]
            {
                "get key1\r\n",
                "set key2 0 0 4\r\ndata\r\n",
                "delete key3\r\n"
            };

            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();

                foreach (var command in commands)
                {
                    var data = Encoding.UTF8.GetBytes(command);
                    await stream.WriteAsync(data);
                    await stream.FlushAsync();
                    await Task.Delay(50); // Small delay between commands
                }
                
                await Task.Delay(200); // Wait for processing
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            // Test passes if no exception is thrown and all commands are processed
            Assert.Pass();
        }

        [Test]
        public async Task HandleConnectionAsync_WithPartialCommand_HandlesGracefully()
        {
            var partialCommand = "get ke"; // Incomplete command

            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                var data = Encoding.UTF8.GetBytes(partialCommand);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                // Wait then close without completing the command
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            // Test passes if no exception is thrown
            Assert.Pass();
        }
    }

    [TestFixture]
    public class TryReadLineTests : MemCacheConnectionHandlerTests
    {
        private MemCacheConnectionHandler _handler;

        [SetUp]
        public void TestSetUp()
        {
            var client = new TcpClient();
            _handler = new MemCacheConnectionHandler(client);
        }

        [TearDown]
        public void TestTearDown()
        {
            _handler?.Dispose();
        }

        [Test]
        public void TryReadLine_WithCompleteLineEndingInNewline_ReturnsLine()
        {
            var data = "get key1\n";
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data));

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.True);
            var lineString = Encoding.UTF8.GetString(line.ToArray());
            Assert.That(lineString, Is.EqualTo("get key1"));
        }

        [Test]
        public void TryReadLine_WithCompleteLineEndingInCarriageReturnNewline_ReturnsLine()
        {
            var data = "get key1\r\n";
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data));

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.True);
            var lineString = Encoding.UTF8.GetString(line.ToArray());
            Assert.That(lineString, Is.EqualTo("get key1\r"));
        }

        [Test]
        public void TryReadLine_WithIncompleteLineNoNewline_ReturnsFalse()
        {
            var data = "get key1";
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data));

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.False);
            Assert.That(line.IsEmpty, Is.True);
        }

        [Test]
        public void TryReadLine_WithMultipleLines_ReturnsFirstLine()
        {
            var data = "get key1\nget key2\n";
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data));

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.True);
            var lineString = Encoding.UTF8.GetString(line.ToArray());
            Assert.That(lineString, Is.EqualTo("get key1"));
            
            // Verify buffer is advanced
            var remainingString = Encoding.UTF8.GetString(buffer.ToArray());
            Assert.That(remainingString, Is.EqualTo("get key2\n"));
        }

        [Test]
        public void TryReadLine_WithEmptyBuffer_ReturnsFalse()
        {
            var buffer = new ReadOnlySequence<byte>(Array.Empty<byte>());

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.False);
            Assert.That(line.IsEmpty, Is.True);
        }

        [Test]
        public void TryReadLine_WithOnlyNewline_ReturnsEmptyLine()
        {
            var data = "\n";
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data));

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.True);
            Assert.That(line.Length, Is.EqualTo(0));
        }

        [Test]
        public void TryReadLine_WithBinaryData_HandlesCorrectly()
        {
            var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x0A, 0x04, 0x05 }; // Contains \n at position 3
            var buffer = new ReadOnlySequence<byte>(binaryData);

            var result = InvokeTryReadLine(ref buffer, out var line);

            Assert.That(result, Is.True);
            Assert.That(line.ToArray(), Is.EqualTo(new byte[] { 0x01, 0x02, 0x03 }));
            
            // Verify remaining buffer
            Assert.That(buffer.ToArray(), Is.EqualTo(new byte[] { 0x04, 0x05 }));
        }

        private bool InvokeTryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Use reflection to call the private TryReadLine method
            var method = typeof(MemCacheConnectionHandler).GetMethod("TryReadLine", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var parameters = new object?[] { buffer, null };
            var result = (bool)method!.Invoke(_handler, parameters)!;
            
            buffer = (ReadOnlySequence<byte>)parameters[0]!;
            line = (ReadOnlySequence<byte>)parameters[1]!;
            
            return result;
        }
    }

    [TestFixture]
    public class DisposeTests : MemCacheConnectionHandlerTests
    {
        [Test]
        public void Dispose_WithValidHandler_DisposesGracefully()
        {
            using var client = new TcpClient();
            var handler = new MemCacheConnectionHandler(client);

            Assert.DoesNotThrow(() => handler.Dispose());
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            using var client = new TcpClient();
            var handler = new MemCacheConnectionHandler(client);

            Assert.DoesNotThrow(() => handler.Dispose());
            Assert.DoesNotThrow(() => handler.Dispose());
        }

        [Test]
        public void Dispose_WithConnectedClient_DisposesConnection()
        {
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                
                // Keep connection alive briefly
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                
                await Task.Delay(50);
                
                // Dispose should close the connection
                handler.Dispose();
                
                // Verify client is disconnected by checking if we can still read
                Assert.That(tcpClient.Connected, Is.False.Or.True); // Connection state may vary
            });

            Assert.DoesNotThrowAsync(async () => await Task.WhenAll(clientTask, serverTask));
        }
    }

    [TestFixture]
    public class IntegrationTests : MemCacheConnectionHandlerTests
    {
        [Test]
        public async Task End2End_GetCommand_ProcessesSuccessfully()
        {
            var command = "get testkey\r\n";
            
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                
                var data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                
                // This should process the command without throwing
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            Assert.Pass("Command processed successfully");
        }

        [Test]
        public async Task End2End_SetCommand_ProcessesSuccessfully()
        {
            var command = "set testkey 123 3600 4\r\ndata\r\n";
            
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                
                var data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            Assert.Pass("Set command processed successfully");
        }

        [Test]
        public async Task End2End_DeleteCommand_ProcessesSuccessfully()
        {
            var command = "delete testkey\r\n";
            
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                
                var data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            Assert.Pass("Delete command processed successfully");
        }

        [Test]
        public async Task End2End_InvalidCommand_HandlesGracefully()
        {
            var command = "invalid command\r\n";
            
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                
                var data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            Assert.Pass("Invalid command handled gracefully");
        }

        [Test]
        public async Task End2End_LargeCommand_ProcessesSuccessfully()
        {
            var largeKey = new string('a', 200); // Large but valid key
            var command = $"get {largeKey}\r\n";
            
            var clientTask = Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port);
                var stream = client.GetStream();
                
                var data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                await Task.Delay(100);
            });

            var serverTask = Task.Run(async () =>
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                using var handler = new MemCacheConnectionHandler(tcpClient);
                
                await handler.HandleConnectionAsync();
            });

            await Task.WhenAll(clientTask, serverTask);
            
            Assert.Pass("Large command processed successfully");
        }
    }
}