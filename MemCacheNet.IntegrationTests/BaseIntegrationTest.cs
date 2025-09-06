using System.Net.Sockets;
using System.Text;

namespace MemCacheNet.IntegrationTests;

public abstract class BaseIntegrationTest : IDisposable
{
    protected const string DefaultHost = "localhost";
    protected const int DefaultPort = 11211;
    protected TcpClient? _client;
    protected NetworkStream? _stream;

    protected async Task<TcpClient> ConnectAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(DefaultHost, DefaultPort);
        _stream = _client.GetStream();
        return _client;
    }

    protected async Task<string> SendCommandAsync(string command)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected to server");

        var commandBytes = Encoding.UTF8.GetBytes(command);
        await _stream.WriteAsync(commandBytes);

        var buffer = new byte[4096];
        var bytesRead = await _stream.ReadAsync(buffer);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    protected async Task<string> SendSetCommandAsync(string key, string value, int flags = 0, int expiration = 0)
    {
        var setCommand = $"set {key} {flags} {expiration} {value.Length}\r\n{value}\r\n";
        return await SendCommandAsync(setCommand);
    }

    protected async Task<string> SendGetCommandAsync(string key)
    {
        var getCommand = $"get {key}\r\n";
        return await SendCommandAsync(getCommand);
    }

    protected async Task<string> SendGetCommandAsync(params string[] keys)
    {
        var getCommand = $"get {string.Join(" ", keys)}\r\n";
        return await SendCommandAsync(getCommand);
    }

    protected async Task<string> SendDeleteCommandAsync(string key)
    {
        var deleteCommand = $"delete {key}\r\n";
        return await SendCommandAsync(deleteCommand);
    }

    protected async Task<string> SendDeleteCommandAsync(string key, bool noReply, bool expectAnswer = false)
    {
        var deleteCommand = noReply ? $"delete {key} noreply\r\n" : $"delete {key}\r\n";
        
        if (noReply && !expectAnswer)
        {
            return await SendNoReplyCommandAsync(deleteCommand);
        }
        
        return await SendCommandAsync(deleteCommand);
    }

    protected async Task<string> SendNoReplyCommandAsync(string command)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected to server");

        var commandBytes = Encoding.UTF8.GetBytes(command);
        await _stream.WriteAsync(commandBytes);

        // For noreply commands, we don't expect a response
        // Give a small delay to ensure the command is processed
        await Task.Delay(50);
        
        return string.Empty;
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }
}