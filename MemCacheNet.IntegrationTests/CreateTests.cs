using System.Net.Sockets;
using System.Text;
using FluentAssertions;

public class CreateTests
{
    public CreateTests()
    {
    }

    [Fact]
    public async Task CreateMemeCacheEntry_ShouldSucceed()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 11211);
        using var stream = client.GetStream();
        var key = $"key";
        var value = $"value";

        // Set value
        var setCommand = $"set {key} 0 0 {value.Length}\r\n{value}\r\n";
        var setCommandBytes = Encoding.UTF8.GetBytes(setCommand);
        await stream.WriteAsync(setCommandBytes);

        var setResponse = new byte[1024];
        var setBytesRead = await stream.ReadAsync(setResponse);
        var setResponseText = Encoding.UTF8.GetString(setResponse, 0, setBytesRead);

        setResponseText.Should().Contain("STORED");

        var getCommand = $"get {key}\r\n";
        var getCommandBytes = Encoding.UTF8.GetBytes(getCommand);
        await stream.WriteAsync(getCommandBytes);

        var getResponse = new byte[1024];
        var getBytesRead = await stream.ReadAsync(getResponse);
        var getResponseText = Encoding.UTF8.GetString(getResponse, 0, getBytesRead);
        
        getResponseText.Should().Contain($"VALUE {key} 0 {value.Length}");
        getResponseText.Should().Contain(value);

    }
}