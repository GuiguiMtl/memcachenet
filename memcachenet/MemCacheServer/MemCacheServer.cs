using System.Buffers;
using System.Diagnostics;
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
    private readonly MemCacheServerSettings _settings;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Constructor for production use with dependency injection.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="memCacheServerSettings">The server configuration settings.</param>
    public MemCacheServer(ILogger<MemCacheServer> logger,
        IOptions<MemCacheServerSettings> memCacheServerSettings,
        ILoggerFactory loggerFactory)
        : this(memCacheServerSettings.Value, logger, loggerFactory)
    {
    }

    /// <summary>
    /// Constructor for testing purposes that accepts settings directly.
    /// </summary>
    /// <param name="settings">The server configuration settings.</param>
    /// <param name="logger">Optional logger instance, uses null logger if not provided.</param>
    public MemCacheServer(MemCacheServerSettings settings, ILogger<MemCacheServer>? logger = null, ILoggerFactory? loggerFactory = null)
    {
        _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MemCacheServer>.Instance;
        _loggerFactory = loggerFactory;
        _parser = new MemCacheCommandParser(settings.MaxKeySizeBytes, settings.MaxDataSizeBytes);
        _memCache = new MemCacheBuilder()
                            .WithExpirationTime(
                                TimeSpan.FromSeconds(settings.ExpirationTimeSeconds))
                            .WithMaxKeys(settings.MaxKeys)
                            .WithMaxCacheSize(settings.MaxTotalCacheSizeBytes).Build();
        _commandHandler = new MemCacheCommandHandler(_memCache, _loggerFactory?.CreateLogger<MemCacheCommandHandler>());
        _connectionSemaphore = new SemaphoreSlim(settings.MaxConcurrentConnections);
        _listener = new(IPAddress.Any, settings.Port);
    }

    public IMemCache MemCache => _memCache;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _logger.LogInformation("MemCache server started on port {Port}", _settings.Port);

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
                var tcpClient = await _listener.AcceptTcpClientAsync(token);
                var connectionId = Guid.NewGuid().ToString("N")[..8];
                var clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
                
                _logger.LogInformation("Accepted connection {ConnectionId} from {ClientEndpoint}", connectionId, clientEndpoint);
                
                var connectionHandlerLogger = _loggerFactory?.CreateLogger<MemCacheConnectionHandler>();
                var connectionHandler = new MemCacheConnectionHandler(
                    tcpClient, 
                    (line, writeResponse) => OnCommandLineRead(line, writeResponse, connectionId),
                    connectionHandlerLogger);
                
                // Start connection session span and handle connection asynchronously
                _ = Task.Run(async () =>
                {
                    using var sessionActivity = MemCacheTelemetry.ActivitySource.StartActivity(MemCacheTelemetry.ActivityNames.ConnectionSession);
                    sessionActivity?.SetTag(MemCacheTelemetry.Tags.ConnectionId, connectionId);
                    sessionActivity?.SetTag(MemCacheTelemetry.Tags.ClientEndpoint, clientEndpoint);
                    
                    try
                    {
                        await connectionHandler.HandleConnectionAsync();
                    }
                    catch (Exception ex)
                    {
                        sessionActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        sessionActivity?.SetTag(MemCacheTelemetry.Tags.ErrorType, ex.GetType().Name);
                        _logger.LogError(ex, "Error handling connection {ConnectionId}: {ErrorMessage}", connectionId, ex.Message);
                    }
                    finally
                    {
                        _logger.LogInformation("Connection {ConnectionId} closed", connectionId);
                        connectionHandler.Dispose();
                        _connectionSemaphore.Release();
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cancellation requested. Stopping listener.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client connection");
                _connectionSemaphore.Release();
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MemCache server on port {Port}", _settings.Port);
        _listener.Stop();
        return Task.CompletedTask;
    }

    private async void OnCommandLineRead(ReadOnlySequence<byte> line, Func<byte[], Task> writeResponse, string connectionId)
    {
        using var activity = MemCacheTelemetry.ActivitySource.StartActivity(MemCacheTelemetry.ActivityNames.CommandHandle);
        activity?.SetTag(MemCacheTelemetry.Tags.ConnectionId, connectionId);
        
        try
        {
            var command = _parser.ParseCommand(line);
            activity?.SetTag(MemCacheTelemetry.Tags.CommandType, command.GetType().Name);
            
            var response = await command.HandleAsync(_commandHandler);
            await writeResponse(response);
            
            _logger.LogDebug("Processed command {CommandType} for connection {ConnectionId}", 
                command.GetType().Name, connectionId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag(MemCacheTelemetry.Tags.ErrorType, ex.GetType().Name);
            
            _logger.LogError(ex, "Error processing command for connection {ConnectionId}", connectionId);
            
            // Send error response
            var errorResponse = System.Text.Encoding.UTF8.GetBytes("ERROR\r\n");
            await writeResponse(errorResponse);
        }
    }
}