using System.Text;

/// <summary>
/// Provides formatting utilities for MemCache command responses according to the memcached protocol.
/// </summary>
public static class ResponseFormatter
{
    /// <summary>
    /// Formats a GET command response into the memcached protocol byte format.
    /// </summary>
    /// <param name="response">The GET command response to format.</param>
    /// <returns>A byte array containing the formatted response ready to send to the client.</returns>
    public static byte[] FormatMemCacheCommandResponse(GetMemCacheCommandResponse response)
    {
        if (!response.Success)
        {
            return Encoding.UTF8.GetBytes($"SERVER_ERROR {response.ErrorMessage}");
        }
        var responses = new byte[response.Values.Count][];
        var endingBytes = Encoding.UTF8.GetBytes("END\r\n");
        int index = 0;
        int totalItemBytesSize = 0;
        foreach (var item in response.Values)
        {
            responses[index] = FormatMemCacheItem(item);
            totalItemBytesSize += responses[index].Length;
            index++;
        }
        var reponseTotalBytes = totalItemBytesSize + endingBytes.Length;
        var responseBytesArray = new byte[reponseTotalBytes];
        int currentResponseIndex = 0;

        for (int i = 0; i < responses.Length; i++)
        {
            Array.Copy(responses[i], 0, responseBytesArray, currentResponseIndex, responses[i].Length);
            currentResponseIndex += responses[i].Length;
        }

        Array.Copy(endingBytes, 0, responseBytesArray, currentResponseIndex, endingBytes.Length);

        return responseBytesArray;
    }

    /// <summary>
    /// Formats a SET command response into the memcached protocol byte format.
    /// </summary>
    /// <param name="response">The SET command response to format.</param>
    /// <param name="noReply">Whether the client requested no reply (noreply flag).</param>
    /// <returns>A byte array containing the formatted response, or empty array if noReply is true and successful.</returns>
    public static byte[] FormatMemCacheCommandResponse(SetMemCacheCommandResponse response, bool noReply) => FormatMemCacheCommandResponse(response, noReply, "STORED\r\n");

    /// <summary>
    /// Formats a DELETE command response into the memcached protocol byte format.
    /// </summary>
    /// <param name="response">The DELETE command response to format.</param>
    /// <param name="noReply">Whether the client requested no reply (noreply flag).</param>
    /// <returns>A byte array containing the formatted response, or empty array if noReply is true and successful.</returns>
    public static byte[] FormatMemCacheCommandResponse(DeleteMemeCacheCommandReponse response, bool noReply) => FormatMemCacheCommandResponse(response, noReply, "DELETED\r\n");

    /// <summary>
    /// Internal helper method to format generic MemCache command responses.
    /// </summary>
    /// <param name="response">The command response to format.</param>
    /// <param name="noReply">Whether the client requested no reply (noreply flag).</param>
    /// <param name="returnMessage">The success message to return if the command succeeded.</param>
    /// <returns>A byte array containing the formatted response.</returns>
    private static byte[] FormatMemCacheCommandResponse(MemCacheResponse response, bool noReply, string returnMessage)
    {
        if (response.Success)
        {
            if (noReply)
            {
                return [];
            }
            return Encoding.UTF8.GetBytes(returnMessage);
        }

        return Encoding.UTF8.GetBytes($"SERVER_ERROR {response.ErrorMessage}");
    }

    /// <summary>
    /// Formats a single cache item into the memcached protocol VALUE response format.
    /// </summary>
    /// <param name="value">The cache value to format.</param>
    /// <returns>A byte array containing the formatted VALUE line followed by data and CRLF terminator.</returns>
    private static byte[] FormatMemCacheItem(MemCacheValue value)
    {
        var byteHeader = Encoding.UTF8.GetBytes($"VALUE ${value.Key} ${value.Flags} ${value.Bytes} \r\n");
        var byteLineEnding = Encoding.UTF8.GetBytes("\r\n");
        // Initialize an array for the resopnse of the size of the header + the data + 2 for the final \r\n line ending
        byte[] bytesResponse = new byte[byteHeader.Length + value.Bytes + 2];
        Array.Copy(byteHeader, 0, bytesResponse, 0, byteHeader.Length);
        Array.Copy(value.Data, 0, bytesResponse, byteHeader.Length, value.Data.Length);
        Array.Copy(byteLineEnding, 0, bytesResponse, byteHeader.Length + value.Data.Length, 2);
        return bytesResponse;
    }
}