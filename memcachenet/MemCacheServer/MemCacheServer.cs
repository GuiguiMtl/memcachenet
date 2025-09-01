using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace memcachenet.MemCacheServer;

public class MemCacheServer : IHostedService
{
    private readonly TcpListener _listener;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly IMemCache _memCache;
    private readonly MemCacheCommandParser _parser;
    private readonly MemCacheCommandHandler _commandHandler;
    private readonly ILogger<MemCacheServer> logger;
    private readonly IOptions<MemCacheServerSettings> memCacheServerSettings;

    public MemCacheServer(ILogger<MemCacheServer> logger,
        IOptions<MemCacheServerSettings> memCacheServerSettings,
        MemCacheCommandParser parser)
    {
        this.logger = logger;
        this.memCacheServerSettings = memCacheServerSettings;
        this._parser = parser;
        _memCache = new MemCacheBuilder()
                            .WithExpirationTime(
                                TimeSpan.FromSeconds(
                                    memCacheServerSettings.Value.ExpirationTimeSeconds))
                            .WithMaxKeys(memCacheServerSettings.Value.MaxKeys)
                            .WithMaxCacheSize(memCacheServerSettings.Value.MaxKeySizeBytes).Build();
        this._commandHandler = new MemCacheCommandHandler(_memCache);
        this._connectionSemaphore = new SemaphoreSlim(memCacheServerSettings.Value.MaxConcurrentConnections);
        this._listener = new(IPAddress.Any, memCacheServerSettings.Value.Port);
    }

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
                var connectionHandler = new MemCacheConnectionHandler(
                    await _listener.AcceptTcpClientAsync(token), 
                    OnCommandLineRead);
                _ = connectionHandler.HandleConnectionAsync().ContinueWith(_ => connectionHandler.Dispose());
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

    private async void OnCommandLineRead(ReadOnlySequence<byte> line, Func<byte[], Task> writeResponse)
    {
        var command = _parser.ParseCommand(line);
        var response = await command.HandleAsync(_commandHandler);
        await writeResponse(response);
    }
}