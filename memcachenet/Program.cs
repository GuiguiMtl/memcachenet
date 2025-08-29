using memcachenet.MemCacheServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace memcachenet;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Register your server as a singleton service and a hosted service
        builder.Services.AddSingleton<MemCacheServer.MemCacheServer>();
        builder.Services.AddSingleton<ExpirationManagerService>();

        builder.Services.AddHostedService(p => p.GetRequiredService<MemCacheServer.MemCacheServer>());
        builder.Services.AddHostedService(p => p.GetRequiredService<ExpirationManagerService>());
        

        var app = builder.Build();

        // 1. Start the host. This calls StartAsync on all IHostedService instances.
        await app.StartAsync();

        // The server is now running in the background.

        // 2. Run the CLI on the main thread.
        RunCommandLineInterface(app.Services);

        // 3. Stop the host gracefully when the CLI exits.
        await app.StopAsync();
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