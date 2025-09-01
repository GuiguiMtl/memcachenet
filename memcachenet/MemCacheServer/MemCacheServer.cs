using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace memcachenet.MemCacheServer;

public class MemCacheServer(ILogger<MemCacheServer> logger,
IOptions<MemCacheServerSettings> memCacheServerSettings,
MemCacheCommandParser parser,
MemCacheCommandHandler commandHandler) : IHostedService
{
    private readonly TcpListener _listener = new(IPAddress.Any, memCacheServerSettings.Value.Port);
    private readonly SemaphoreSlim _connectionSemaphore = new(memCacheServerSettings.Value.MaxConcurrentConnections);
    private readonly IMemCache _memCache = new MemCacheBuilder()
        .WithExpirationTime(TimeSpan.FromSeconds(memCacheServerSettings.Value.ExpirationTimeSeconds))
        .WithMaxKeys(memCacheServerSettings.Value.MaxKeys)
        .Build();
    private readonly IOptions<MemCacheServerSettings> memCacheServerSettings = memCacheServerSettings;
    private readonly MemCacheCommandParser parser = parser;
    private readonly MemCacheCommandHandler commandHandler = commandHandler;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        logger.LogInformation("Server started on port 11211.");

        Task.Run(() => AcceptClientsAsync(cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await _connectionSemaphore.WaitAsync(token);
            try
            {
                using var connectionHandler = new MemCacheConnectionHandler(
                    await _listener.AcceptTcpClientAsync(token), 
                    OnCommandLineRead);
                _ = connectionHandler.HandleConnectionAsync();
            }
            catch (OperationCanceledException)
            {
                // This is expected when the host is shutting down.
                logger.LogInformation("Cancellation requested. Stopping listener.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping server.");
        _listener.Stop();
        return Task.CompletedTask;
    }

    private void OnCommandLineRead(ReadOnlySequence<byte> line)
    {
        var command  = parser.ParseCommand(line);
        command.Handle(commandHandler);
    }
}