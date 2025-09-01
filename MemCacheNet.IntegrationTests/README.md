# MemCache Integration Tests

This project contains comprehensive integration tests for the MemCacheNet server implementation. The tests validate the entire stack including TCP communication, protocol parsing, command handling, and response formatting according to the memcached protocol specification.

## Test Structure

### Test Classes

1. **MemCacheProtocolTests** - Core protocol command testing
   - Basic SET, GET, DELETE operations
   - Multi-key GET operations
   - NoReply flag handling
   - Invalid command handling

2. **ConcurrencyTests** - Multi-client and thread safety testing
   - Multiple concurrent client connections
   - Concurrent operations on shared keys
   - Connection limit validation
   - Thread safety verification

3. **EvictionPolicyTests** - LRU eviction policy validation
   - Least Recently Used key eviction
   - Access pattern preservation
   - Update operation LRU positioning
   - Delete operation cleanup

4. **ServerLimitsTests** - Server constraint validation
   - Key size limits (250 bytes per assignment)
   - Value size limits (100KB per assignment)
   - Maximum key count (3000 per assignment)
   - Special character handling in keys

5. **ErrorHandlingTests** - Error conditions and edge cases
   - Malformed command handling
   - Invalid parameter validation
   - Client disconnection scenarios
   - Binary data support
   - Concurrent error conditions

## Assignment Requirements Validation

These tests validate compliance with the assignment requirements:

### Protocol Implementation (25%)
- ✅ GET command with single and multiple keys
- ✅ SET command with flags, expiration, and data
- ✅ DELETE command with proper responses
- ✅ NoReply flag support for SET and DELETE
- ✅ Proper CRLF line termination handling
- ✅ Error responses (CLIENT_ERROR, SERVER_ERROR, NOT_FOUND)

### Limits Enforcement
- ✅ Key size limit: 250 bytes (configurable in tests)
- ✅ Value size limit: 102400 bytes / 100KB (configurable in tests)
- ✅ Max keys: 3000 (configurable in tests)
- ✅ Command rejection when limits are exceeded

### Concurrency Support (20%)
- ✅ Multiple concurrent client connections
- ✅ Thread-safe operations on shared data
- ✅ Proper connection handling and cleanup

### Eviction Policy (Basic requirement)
- ✅ LRU (Least Recently Used) implementation
- ✅ Automatic eviction when key limit is reached
- ✅ Access pattern tracking and maintenance

### Error Handling (20%)
- ✅ Malformed command rejection
- ✅ Invalid parameter handling
- ✅ Graceful client disconnection handling
- ✅ Server stability under error conditions

## Running the Tests

### Prerequisites
- .NET 9.0 SDK
- MemCacheNet main project built successfully

### Command Line
```bash
# Run all integration tests
dotnet test MemCacheNet.IntegrationTests

# Run specific test class
dotnet test MemCacheNet.IntegrationTests --filter "FullyQualifiedName~MemCacheProtocolTests"

# Run with detailed output
dotnet test MemCacheNet.IntegrationTests --logger "console;verbosity=detailed"
```

### Test Configuration

Each test class uses a different port to avoid conflicts:
- MemCacheProtocolTests: Port 11212
- ConcurrencyTests: Port 11213  
- EvictionPolicyTests: Port 11214
- ServerLimitsTests: Port 11215
- ErrorHandlingTests: Port 11216

Tests use smaller limits than production for faster execution:
- MaxKeys: Varies by test (5-100 vs production 3000)
- MaxDataSizeBytes: 1024 bytes (vs production 102400)
- MaxKeySizeBytes: 50 bytes (vs production 250)

## Test Dependencies

- **xUnit**: Testing framework
- **FluentAssertions**: Assertion library for readable test assertions
- **Microsoft.Extensions.Hosting**: For async lifecycle management
- **System.Net.Sockets**: For direct TCP client communication

## Test Implementation Notes

### Async Lifecycle Management
All test classes implement `IAsyncLifetime` to:
- Start a dedicated server instance before tests
- Clean up server resources after tests
- Avoid port conflicts between test classes

### Direct TCP Communication
Tests use raw TCP sockets to:
- Validate actual protocol implementation
- Test binary data handling
- Verify exact response formatting
- Simulate real client behavior

### Error Condition Testing
Comprehensive error testing includes:
- Protocol violation scenarios
- Resource limit exceeded conditions
- Network failure simulations
- Concurrent error handling

## Coverage Areas

These integration tests provide coverage for:
- End-to-end protocol implementation
- Multi-client concurrency scenarios
- Resource management and limits
- Error handling and recovery
- Performance under load
- Memory management (eviction policies)

The tests complement unit tests by validating the complete system behavior rather than individual components.