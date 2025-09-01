using System.Buffers;

public interface ICommandParser<TParsingType>
{
    TParsingType ParseCommand(ReadOnlySequence<byte> buffer);
}