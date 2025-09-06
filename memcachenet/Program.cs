using memcachenet.MemCacheServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;

namespace memcachenet;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(args);

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
                            });
                        }
                    });
            }

            builder.Services.AddSingleton<MemCacheServer.MemCacheServer>();
            builder.Services.AddSingleton<ExpirationManagerService>();
            builder.Services.AddSingleton<MemCacheCommandParser>();

            builder.Services.AddHostedService(p => p.GetRequiredService<MemCacheServer.MemCacheServer>());
            // builder.Services.AddHostedService(p => p.GetRequiredService<ExpirationManagerService>());

            var app = builder.Build();

            // 1. Start the host. This calls StartAsync on all IHostedService instances.
            await app.StartAsync();

            // The server is now running in the background.

            // 2. Run the CLI on the main thread.
            // Here we could handle commands that would be done directly to the mem cache. Out of Scope for this project
            RunCommandLineInterface(app.Services);

            // 3. Stop the host gracefully when the CLI exits.
            await app.StopAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    private static void RunCommandLineInterface(IServiceProvider services)
    {
        // Retrieve the running server instance from the DI container
        var server = services.GetRequiredService<MemCacheServer.MemCacheServer>();

        Console.WriteLine("Server is running. Type 'stats' or 'exit'.");
    
        // This CLI loop is almost identical to the previous version
        while (true)
        {
            Console.Write("> ");
            string input = Console.ReadLine();
            if (input == "exit") break;
            if (input == "stats")
            {
                // We can now call public methods on our server instance
                // Note: GetStatsAsync should be updated to work in this context
                Console.WriteLine("Fetching stats...");
            }
        }
    }
}