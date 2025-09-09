using System.Buffers;
using System.Text;
using memcachenet.MemCacheServer;
using memcachenet.MemCacheServer.Commands;

namespace MemCacheNetTests;

[TestFixture]
public class MemCacheCommandParserTests
{
    private MemCacheCommandParser _parser;
    private const int MaxKeySize = 250;
    private const int MaxDataSize = 1024 * 1024;

    [SetUp]
    public void SetUp()
    {
        _parser = new MemCacheCommandParser(MaxKeySize, MaxDataSize);
    }

    [TestFixture]
    public class ParseCommandTests : MemCacheCommandParserTests
    {
        [Test]
        public void ParseCommand_InvalidCommand_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("unknown key\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void ParseCommand_EmptyBuffer_ReturnsInvalidCommand()
        {
            var buffer = new ReadOnlySequence<byte>(Array.Empty<byte>());
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void ParseCommand_NoSpaceAfterCommand_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("get\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void ParseCommand_GetCommand_ReturnsGetCommand()
        {
            var buffer = CreateBuffer("get key1\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<GetMemCacheCommand>());
        }

        [Test]
        public void ParseCommand_SetCommand_ReturnsSetCommand()
        {
            var buffer = CreateBuffer("set key1 0 0 4\r\ndata\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<SetMemCacheCommand>());
        }

        [Test]
        public void ParseCommand_DeleteCommand_ReturnsDeleteCommand()
        {
            var buffer = CreateBuffer("delete key1\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<DeleteMemCacheCommand>());
        }
    }

    [TestFixture]
    public class HandleGetCommandTests : MemCacheCommandParserTests
    {
        [Test]
        public void HandleGetCommand_SingleKey_ReturnsCorrectCommand()
        {
            var buffer = CreateBuffer("get key1\r\n");
            
            var result = _parser.ParseCommand(buffer) as GetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Keys, Has.Length.EqualTo(1));
            Assert.That(result.Keys[0], Is.EqualTo("key1"));
        }

        [Test]
        public void HandleGetCommand_MultipleKeys_ReturnsCorrectCommand()
        {
            var buffer = CreateBuffer("get key1 key2 key3\r\n");
            
            var result = _parser.ParseCommand(buffer) as GetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Keys, Has.Length.EqualTo(3));
            Assert.That(result.Keys[0], Is.EqualTo("key1"));
            Assert.That(result.Keys[1], Is.EqualTo("key2"));
            Assert.That(result.Keys[2], Is.EqualTo("key3"));
        }

        [Test]
        public void HandleGetCommand_KeyTooLong_ReturnsInvalidCommand()
        {
            var longKey = new string('a', MaxKeySize + 1);
            var buffer = CreateBuffer($"get {longKey}\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleGetCommand_NoEndLineMarkers_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("get key1");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleGetCommand_OnlyCarriageReturn_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("get key1\r");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleGetCommand_OnlyNewline_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("get key1\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleGetCommand_EmptyKeys_FiltersEmptyKeys()
        {
            var buffer = CreateBuffer("get key1  key2\r\n");
            
            var result = _parser.ParseCommand(buffer) as GetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Keys, Has.Length.EqualTo(2));
            Assert.That(result.Keys[0], Is.EqualTo("key1"));
            Assert.That(result.Keys[1], Is.EqualTo("key2"));
        }
        
        [Test]
        public void HandleGetCommand_MaxKeySizeKey_ReturnsValidCommand()
        {
            var maxKey = new string('a', MaxKeySize);
            var buffer = CreateBuffer($"get {maxKey}\r\n");
            
            var result = _parser.ParseCommand(buffer) as GetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Keys[0], Is.EqualTo(maxKey));
        }
    }

    [TestFixture]
    public class HandleSetCommandTests : MemCacheCommandParserTests
    {
        [Test]
        public void HandleSetCommand_ValidCommand_ReturnsCorrectCommand()
        {
            var data = "test";
            var buffer = CreateBuffer($"set key1 123 3600 {data.Length}\r\n{data}\r\n");
            
            var result = _parser.ParseCommand(buffer) as SetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo("key1"));
            Assert.That(result.Flags, Is.EqualTo(123u));
            Assert.That(result.Expiration, Is.EqualTo(3600));
            Assert.That(result.Data, Is.EqualTo(Encoding.UTF8.GetBytes(data)));
            Assert.That(result.NoReply, Is.False);
        }

        [Test]
        public void HandleSetCommand_WithNoReply_ReturnsCorrectCommand()
        {
            var data = "test";
            var buffer = CreateBuffer($"set key1 123 3600 {data.Length} noreply\r\n{data}\r\n");
            
            var result = _parser.ParseCommand(buffer) as SetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo("key1"));
            Assert.That(result.Flags, Is.EqualTo(123u));
            Assert.That(result.Expiration, Is.EqualTo(3600));
            Assert.That(result.Data, Is.EqualTo(Encoding.UTF8.GetBytes(data)));
            Assert.That(result.NoReply, Is.True);
        }

        [Test]
        public void HandleSetCommand_InvalidNoReply_ReturnsInvalidCommand()
        {
            var data = "test";
            var buffer = CreateBuffer($"set key1 123 3600 {data.Length} invalid\r\n{data}");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_ExtraParametersAfterNoReply_ReturnsInvalidCommand()
        {
            var data = "test";
            var buffer = CreateBuffer($"set key1 123 3600 {data.Length} noreply extra\r\n{data}");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_DataTooLarge_ReturnsInvalidCommand()
        {
            var largeData = new string('a', MaxDataSize + 1);
            var buffer = CreateBuffer($"set key1 0 0 {largeData.Length}\r\n{largeData}");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_NoData_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1 0 0 4\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_KeyTooLong_ReturnsInvalidCommand()
        {
            var longKey = new string('a', MaxKeySize + 1);
            var buffer = CreateBuffer($"set {longKey} 0 0 4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_InvalidFlag_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1 invalid 0 4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_InvalidExpiration_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1 0 invalid 4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_NoEndLineMarkers_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1 0 0 4");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }
        
        [Test]
        public void HandleSetCommand_MaxDataSize_ReturnsValidCommand()
        {
            var maxData = new string('a', MaxDataSize);
            var buffer = CreateBuffer($"set key1 0 0 {MaxDataSize}\r\n{maxData}\r\n");
            
            var result = _parser.ParseCommand(buffer) as SetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Data, Has.Length.EqualTo(MaxDataSize));
        }

        [Test]
        public void HandleSetCommand_EmptyData_ReturnsValidCommand()
        {
            var buffer = CreateBuffer("set key1 0 0 0\r\n\r\n");
            
            var result = _parser.ParseCommand(buffer) as SetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Data, Has.Length.EqualTo(0));
        }
        
        [Test]
        public void HandleSetCommand_ZeroFlag_ReturnsValidCommand()
        {
            var data = "test";
            var buffer = CreateBuffer($"set key1 0 0 {data.Length}\r\n{data}\r\n");
            
            var result = _parser.ParseCommand(buffer) as SetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Flags, Is.EqualTo(0u));
        }
        
        [Test]
        public void HandleSetCommand_MissingKeyParameter_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set  0 0 4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_MissingFlagParameter_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1  0 4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_MissingExpirationParameter_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1 0  4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        

        [Test]
        public void HandleSetCommand_NegativeExpiration_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("set key1 0 -1 4\r\ndata");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleSetCommand_LargeValidFlag_ReturnsValidCommand()
        {
            var data = "test";
            var buffer = CreateBuffer($"set key1 4294967295 0 {data.Length}\r\n{data}\r\n");
            
            var result = _parser.ParseCommand(buffer) as SetMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Flags, Is.EqualTo(4294967295u));
        }
        
    }

    [TestFixture]
    public class HandleDeleteCommandTests : MemCacheCommandParserTests
    {
        [Test]
        public void HandleDeleteCommand_ValidCommand_ReturnsCorrectCommand()
        {
            var buffer = CreateBuffer("delete key1\r\n");
            
            var result = _parser.ParseCommand(buffer) as DeleteMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo("key1"));
            Assert.That(result.NoReply, Is.False);
        }

        [Test]
        public void HandleDeleteCommand_WithNoReply_ReturnsCorrectCommand()
        {
            var buffer = CreateBuffer("delete key1 noreply\r\n");
            
            var result = _parser.ParseCommand(buffer) as DeleteMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo("key1"));
            Assert.That(result.NoReply, Is.True);
        }

        [Test]
        public void HandleDeleteCommand_InvalidNoReply_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("delete key1 invalid\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleDeleteCommand_KeyTooLong_ReturnsInvalidCommand()
        {
            var longKey = new string('a', MaxKeySize + 1);
            var buffer = CreateBuffer($"delete {longKey}\r\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleDeleteCommand_NoEndLineMarkers_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("delete key1");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleDeleteCommand_OnlyCarriageReturn_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("delete key1\r");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }

        [Test]
        public void HandleDeleteCommand_OnlyNewline_ReturnsInvalidCommand()
        {
            var buffer = CreateBuffer("delete key1\n");
            
            var result = _parser.ParseCommand(buffer);
            
            Assert.That(result, Is.InstanceOf<InvalidMemCacheCommand>());
        }
        
        [Test]
        public void HandleDeleteCommand_MaxKeySizeKey_ReturnsValidCommand()
        {
            var maxKey = new string('a', MaxKeySize);
            var buffer = CreateBuffer($"delete {maxKey}\r\n");
            
            var result = _parser.ParseCommand(buffer) as DeleteMemCacheCommand;
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo(maxKey));
        }
    }

    [TestFixture]
    public class HelperMethodTests : MemCacheCommandParserTests
    {
        

        

        

        
    }

    private static ReadOnlySequence<byte> CreateBuffer(string command)
    {
        var bytes = Encoding.UTF8.GetBytes(command);
        return new ReadOnlySequence<byte>(bytes);
    }
}