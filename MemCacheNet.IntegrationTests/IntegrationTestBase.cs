using System.Diagnostics;
using System.Net.Sockets;

namespace MemCacheNet.IntegrationTests;

/// <summary>
/// Base class for integration tests that need a running memcachenet server instance.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private Process? _serverProcess;
    protected readonly int TestPort;
    
    protected IntegrationTestBase(int testPort)
    {
        TestPort = testPort;
    }
    
    /// <summary>
    /// Sets up the test server before running tests.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Find the solution directory by looking for the .sln file
        var currentDir = Environment.CurrentDirectory;
        var solutionDir = FindSolutionDirectory(currentDir);
        var projectPath = Path.Combine(solutionDir, "memcachenet", "memcachenet.csproj");
        
        // Build the project first to ensure we have the latest executable
        var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build --configuration Debug \"{projectPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        
        buildProcess.Start();
        await buildProcess.WaitForExitAsync();
        
        if (buildProcess.ExitCode != 0)
        {
            var output = await buildProcess.StandardOutput.ReadToEndAsync();
            var error = await buildProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to build memcachenet project for testing. Output: {output}, Error: {error}");
        }

        // Start the memcachenet application with custom configuration for testing
        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment = 
                {
                    ["MemCacheServerSettings__Port"] = TestPort.ToString(),
                    ["MemCacheServerSettings__MaxKeys"] = GetMaxKeys().ToString(),
                    ["MemCacheServerSettings__MaxDataSizeBytes"] = GetMaxDataSize().ToString(),
                    ["MemCacheServerSettings__MaxKeySizeBytes"] = GetMaxKeySize().ToString(),
                    ["MemCacheServerSettings__MaxConcurrentConnections"] = GetMaxConnections().ToString(),
                    ["MemCacheServerSettings__ExpirationTimeSeconds"] = GetExpirationSeconds().ToString()
                }
            }
        };

        _serverProcess.Start();
        
        // Give the server time to start and bind to the port
        await Task.Delay(2000);
        
        // Verify the server is responding
        await WaitForServerToStart();
    }

    /// <summary>
    /// Tears down the test server after running tests.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill();
            await _serverProcess.WaitForExitAsync();
            _serverProcess.Dispose();
        }
    }

    private async Task WaitForServerToStart()
    {
        var maxAttempts = 10;
        var delay = 500; // milliseconds

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", TestPort);
                return; // Successfully connected
            }
            catch (SocketException)
            {
                if (attempt == maxAttempts - 1)
                {
                    var output = _serverProcess != null ? await _serverProcess.StandardOutput.ReadToEndAsync() : "";
                    var error = _serverProcess != null ? await _serverProcess.StandardError.ReadToEndAsync() : "";
                    throw new InvalidOperationException($"Server failed to start on port {TestPort} after {maxAttempts} attempts. Output: {output}, Error: {error}");
                }
                await Task.Delay(delay);
            }
        }
    }

    // Virtual methods that can be overridden by derived classes for test-specific configuration
    protected virtual int GetMaxKeys() => 100;
    protected virtual int GetMaxDataSize() => 1024;
    protected virtual int GetMaxKeySize() => 250;
    protected virtual int GetMaxConnections() => 10;
    protected virtual int GetExpirationSeconds() => 3600;

    private static string FindSolutionDirectory(string startPath)
    {
        var currentDir = new DirectoryInfo(startPath);
        
        // Look for .sln file in current and parent directories
        while (currentDir != null)
        {
            if (currentDir.GetFiles("*.sln").Length > 0)
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }
        
        throw new InvalidOperationException($"Could not find solution directory starting from {startPath}");
    }
}