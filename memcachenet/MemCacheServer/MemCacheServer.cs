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
    private readonly ILogger<MemCacheServer> _logger;

    /// <summary>
    /// Constructor for production use with dependency injection.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="memCacheServerSettings">The server configuration settings.</param>
    public MemCacheServer(ILogger<MemCacheServer> logger,
        IOptions<MemCacheServerSettings> memCacheServerSettings)
        : this(memCacheServerSettings.Value, logger)
    {
    }

    /// <summary>
    /// Constructor for testing purposes that accepts settings directly.
    /// </summary>
    /// <param name="settings">The server configuration settings.</param>
    /// <param name="logger">Optional logger instance, uses null logger if not provided.</param>
    public MemCacheServer(MemCacheServerSettings settings, ILogger<MemCacheServer>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MemCacheServer>.Instance;
        _parser = new MemCacheCommandParser(settings.MaxKeySizeBytes, settings.MaxDataSizeBytes);
        _memCache = new MemCacheBuilder()
                            .WithExpirationTime(
                                TimeSpan.FromSeconds(settings.ExpirationTimeSeconds))
                            .WithMaxKeys(settings.MaxKeys)
                            .WithMaxCacheSize(settings.MaxKeySizeBytes).Build();
        _commandHandler = new MemCacheCommandHandler(_memCache);
        _connectionSemaphore = new SemaphoreSlim(settings.MaxConcurrentConnections);
        _listener = new(IPAddress.Any, settings.Port);
    }

    public IMemCache MemCache => _memCache;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _logger.LogInformation("Server started on port 11211.");

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
                _logger.LogInformation("Cancellation requested. Stopping listener.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping server.");
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