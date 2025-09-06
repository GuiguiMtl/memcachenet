# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MemCacheNet is a C# implementation of a memcached server built with .NET 9, implementing a subset of the memcached text protocol (GET, SET, DELETE commands). This is a production-quality implementation designed for high concurrency and performance.

## Project Structure

- **`memcachenet/`** - Main server application (console host)
- **`MemCacheNetTests/`** - Unit tests using NUnit with extensive mocking
- **`MemCacheNet.IntegrationTests/`** - Integration tests using xUnit that test TCP protocol directly
- **`MemCacheNetCLI/`** - Simple command-line client for testing
- **`MemCacheLoadTester/`** - Load testing application

## Development Commands

### Building and Running
```bash
# Build entire solution
dotnet build memcachenet.sln

# Run the server (from memcachenet directory)
dotnet run

# Run with configuration
dotnet run -- --port 11211 --max-connections 1000
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test MemCacheNetTests
dotnet test MemCacheNet.IntegrationTests

# Run specific test
dotnet test --filter "TestMethodName"
dotnet test --filter "ClassName~SetCommandTests"

# Integration tests require the server to be running separately
```

### Docker
```bash
docker build -t memcachenet .
docker run -p 11211:11211 memcachenet
```

## Core Architecture

### Server Infrastructure
- **`MemCacheServer.cs`** - Main server using `IHostedService`, handles TCP listener and connection management
- **`MemCacheConnectionHandler.cs`** - Per-connection handler using `System.IO.Pipelines` for efficient I/O
- **`MemCacheCommandParser.cs`** - Parses memcached text protocol commands from byte sequences

### Cache Implementation
- **`MemCache.cs`** - Thread-safe in-memory cache with LRU eviction policy
- **`CacheItem.cs`** - Represents cached items with metadata (flags, expiration, etc.)
- **Limits**: 1GB total size, 3000 max keys, 250 byte max key size, 100KB max value size

### Command Pattern
Each memcached command is implemented as a separate class:
- **`GetCommand.cs`** - Handles `get <key>` and multi-get
- **`SetCommand.cs`** - Handles `set <key> <flags> <exptime> <bytes> [noreply]`
- **`DeleteCommand.cs`** - Handles `delete <key> [noreply]`

### Key Technical Details
- **Concurrency**: Uses `SemaphoreSlim` to limit concurrent connections
- **Protocol**: Full memcached text protocol with proper `\r\n` termination
- **Memory**: Uses `ReadOnlySequence<byte>` for zero-copy parsing
- **Observability**: OpenTelemetry tracing and structured logging throughout
- **Error Handling**: Proper protocol error responses (`CLIENT_ERROR`, `SERVER_ERROR`)

## Testing Strategy

### Unit Tests (NUnit)
- Extensive mocking of dependencies using NSubstitute
- Test all command classes in isolation
- Cover edge cases, error conditions, and protocol compliance

### Integration Tests (xUnit)
- **Important**: Uses raw TCP connections to test actual protocol
- Tests full request/response cycles including binary data
- **NoReply Handling**: Tests with `noreply` flag use `SendNoReplyCommandAsync()` to avoid hanging on expected empty responses

### Test Patterns
```csharp
// For noreply commands in integration tests
var response = await SendNoReplyCommandAsync(command);
response.Should().BeEmpty();

// For normal commands
var response = await SendCommandAsync(command);
response.Should().Contain("STORED");
```

## Protocol Implementation

### Command Format
```
set <key> <flags> <exptime> <bytes> [noreply]\r\n
<data block>\r\n

get <key> [<key> ...]\r\n

delete <key> [noreply]\r\n
```

### Response Format
```
VALUE <key> <flags> <bytes>\r\n
<data block>\r\n
END\r\n

STORED\r\n
DELETED\r\n
NOT_FOUND\r\n
```

### Error Responses
- `CLIENT_ERROR <message>\r\n` - Invalid command format
- `SERVER_ERROR <message>\r\n` - Internal server errors

## Debugging and Development

### Logging
The application uses structured logging with different levels:
- Server startup/shutdown events
- Connection handling
- Command execution
- Cache operations and evictions

### Telemetry
OpenTelemetry tracing is integrated for monitoring:
- Connection spans
- Command execution spans
- Cache operation spans

### Common Issues
- **Integration test hanging**: Ensure `noreply` tests use `SendNoReplyCommandAsync()`
- **Connection limits**: Server limits concurrent connections via `SemaphoreSlim`
- **Cache eviction**: LRU policy automatically evicts items when limits are exceeded