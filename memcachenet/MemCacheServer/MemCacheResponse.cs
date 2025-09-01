public abstract class MemCacheResponse
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
public class GetMemCacheCommandResponse : MemCacheResponse
{
    public required List<MemCacheValue> Values { get; set; }
}

public class SetMemCacheCommandResponse : MemCacheResponse {}

public class DeleteMemeCacheCommandReponse : MemCacheResponse { }

public class InvalidMemCacheCommandReponse : MemCacheResponse { }

public class MemCacheValue
{
    public required string Key { get; set; }
    public required uint Flags { get; set; }
    public required int Bytes { get; set; }
    public required byte[] Data { get; set; }
}