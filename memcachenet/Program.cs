using memcachenet.MemCacheServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System;

namespace memcachenet;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Delete the existing log file to overwrite it on startup
            var logFilePath = "memcache-server.log";
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }

            var builder = Host.CreateApplicationBuilder(args);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            // Clear default logging providers and add Serilog
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

            builder.Services.Configure<MemCacheServerSettings>(
                builder.Configuration.GetSection("MemCacheServerSettings"));
            builder.Services.Configure<ExpirationManagerSettings>(
                builder.Configuration.GetSection("ExpirationManagerSettings"));
            builder.Services.Configure<OpenTelemetrySettings>(
                builder.Configuration.GetSection("OpenTelemetry"));

            // Configure OpenTelemetry
            var telemetrySettings = builder.Configuration.GetSection("OpenTelemetry").Get<OpenTelemetrySettings>() ?? new OpenTelemetrySettings();
            
            if (telemetrySettings.TracingEnabled)
            {
                builder.Services.AddOpenTelemetry()
                    .ConfigureResource(resource => resource
                        .AddService(telemetrySettings.ServiceName, telemetrySettings.ServiceVersion)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["service.instance.id"] = Environment.MachineName,
                            ["service.version"] = telemetrySettings.ServiceVersion,
                            ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                        }))
                    .WithTracing(tracerProviderBuilder =>
                    {
                        tracerProviderBuilder.AddSource(MemCacheTelemetry.ActivitySourceName);
                        
                        // Configure sampling (using standard samplers)
                        if (telemetrySettings.Sampling.Type.ToLower() == "alwaysoff")
                        {
                            tracerProviderBuilder.SetSampler(new AlwaysOffSampler());
                        }
                        else
                        {
                            tracerProviderBuilder.SetSampler(new AlwaysOnSampler());
                        }
                        
                        // Configure exporters
                        if (telemetrySettings.Exporters.Console.Enabled)
                        {
                            tracerProviderBuilder.AddConsoleExporter(options =>
                            {
                                var consoleSettings = telemetrySettings.Exporters.Console;
                                
                                // Configure console targets
                                if (consoleSettings.Targets.Contains("Debug", StringComparison.OrdinalIgnoreCase))
                                {
                                    options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Debug;
                                }
                                else
                                {
                                    options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
                                }
                            });
                        }
                        
                        if (telemetrySettings.Exporters.OTLP.Enabled)
                        {
                            tracerProviderBuilder.AddOtlpExporter(options =>
                            {
                                options.Endpoint = new Uri(telemetrySettings.Exporters.OTLP.Endpoint);
                                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                            });
                        }
                    });
            }

            builder.Services.AddSingleton<MemCacheServer.MemCacheServer>();
            builder.Services.AddSingleton<ExpirationManagerService>();
            builder.Services.AddSingleton<MemCacheCommandParser>();

            builder.Services.AddHostedService(p => p.GetRequiredService<MemCacheServer.MemCacheServer>());

            // Optionally add the ExpirationManagerService as a hosted service 
            // This will actively remove expired items from the cache - not strictly necessary for functionality
            // builder.Services.AddHostedService(p => p.GetRequiredService<ExpirationManagerService>());

            var app = builder.Build();

            // 1. Start the host. This calls StartAsync on all IHostedService instances.
            await app.StartAsync();

            // 2. Keep the application running until Ctrl+C is pressed
            Log.Information("MemCache server started successfully");
            await app.WaitForShutdownAsync();
            
            Log.Information("MemCache server shutting down...");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "MemCache server terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    private static void RunCommandLineInterface(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Retrieve the running server instance from the DI container
        var server = services.GetRequiredService<MemCacheServer.MemCacheServer>();

        Console.WriteLine("Server is running. Type 'stats' or 'exit', or press Ctrl+C to stop.");
    
        // This CLI loop is almost identical to the previous version
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            
            // Check for cancellation before blocking on ReadLine
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("\nShutdown requested...");
                break;
            }
            
            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input) || input == "exit") break;
            if (input == "stats")
            {
                // We can now call public methods on our server instance
                // Note: GetStatsAsync should be updated to work in this context
                Console.WriteLine("Fetching stats...");
            }
        }
        
        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Shutting down server...");
        }
    }
}