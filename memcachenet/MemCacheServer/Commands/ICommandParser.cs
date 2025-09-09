using System.Buffers;

namespace memcachenet.MemCacheServer.Commands;

public interface ICommandParser<TParsingType>
{
    TParsingType ParseCommand(ReadOnlySequence<byte> buffer);
}