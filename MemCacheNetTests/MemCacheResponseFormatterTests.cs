using System.Text;
using NUnit.Framework;

namespace MemCacheNetTests;

[TestFixture]
public class MemCacheResponseFormatterTests
{
    [Test]
    public void FormatMemCacheCommandResponse_GetResponse_Success_ReturnsCorrectFormat()
    {
        // Arrange
        var memCacheValue = new MemCacheValue
        {
            Key = "testkey",
            Flags = 0,
            Bytes = 9,
            Data = Encoding.UTF8.GetBytes("testvalue")
        };
        
        var response = new GetMemCacheCommandResponse
        {
            Success = true,
            Values = new List<MemCacheValue> { memCacheValue }
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Does.Contain("VALUE testkey 0 9"));
        Assert.That(resultString, Does.Contain("testvalue"));
        Assert.That(resultString, Does.EndWith("END\r\n"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_GetResponse_MultipleValues_ReturnsCorrectFormat()
    {
        // Arrange
        var value1 = new MemCacheValue
        {
            Key = "key1",
            Flags = 0,
            Bytes = 6,
            Data = Encoding.UTF8.GetBytes("value1")
        };
        
        var value2 = new MemCacheValue
        {
            Key = "key2",
            Flags = 1,
            Bytes = 6,
            Data = Encoding.UTF8.GetBytes("value2")
        };

        var response = new GetMemCacheCommandResponse
        {
            Success = true,
            Values = new List<MemCacheValue> { value1, value2 }
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Does.Contain("VALUE key1 0 6"));
        Assert.That(resultString, Does.Contain("value1"));
        Assert.That(resultString, Does.Contain("VALUE key2 1 6"));
        Assert.That(resultString, Does.Contain("value2"));
        Assert.That(resultString, Does.EndWith("END\r\n"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_GetResponse_EmptyValues_ReturnsOnlyEnd()
    {
        // Arrange
        var response = new GetMemCacheCommandResponse
        {
            Success = true,
            Values = new List<MemCacheValue>()
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Is.EqualTo("END\r\n"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_GetResponse_Failure_ReturnsServerError()
    {
        // Arrange
        var response = new GetMemCacheCommandResponse
        {
            Success = false,
            ErrorMessage = "Key not found",
            Values = new List<MemCacheValue>()
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Is.EqualTo("SERVER_ERROR Key not found"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_SetResponse_Success_NoReply_ReturnsEmpty()
    {
        // Arrange
        var response = new SetMemCacheCommandResponse
        {
            Success = true
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response, noReply: true);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void FormatMemCacheCommandResponse_SetResponse_Success_WithReply_ReturnsStored()
    {
        // Arrange
        var response = new SetMemCacheCommandResponse
        {
            Success = true
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response, noReply: false);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Is.EqualTo("STORED\r\n"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_SetResponse_Failure_ReturnsServerError()
    {
        // Arrange
        var response = new SetMemCacheCommandResponse
        {
            Success = false,
            ErrorMessage = "Memory limit exceeded"
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response, noReply: false);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Is.EqualTo("SERVER_ERROR Memory limit exceeded"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_DeleteResponse_Success_NoReply_ReturnsEmpty()
    {
        // Arrange
        var response = new DeleteMemeCacheCommandReponse
        {
            Success = true
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response, noReply: true);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void FormatMemCacheCommandResponse_DeleteResponse_Success_WithReply_ReturnsDeleted()
    {
        // Arrange
        var response = new DeleteMemeCacheCommandReponse
        {
            Success = true
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response, noReply: false);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Is.EqualTo("DELETED\r\n"));
    }

    [Test]
    public void FormatMemCacheCommandResponse_DeleteResponse_Failure_ReturnsServerError()
    {
        // Arrange
        var response = new DeleteMemeCacheCommandReponse
        {
            Success = false,
            ErrorMessage = "Key not found"
        };

        // Act
        var result = ResponseFormatter.FormatMemCacheCommandResponse(response, noReply: false);

        // Assert
        var resultString = Encoding.UTF8.GetString(result);
        Assert.That(resultString, Is.EqualTo("SERVER_ERROR Key not found"));
    }

}