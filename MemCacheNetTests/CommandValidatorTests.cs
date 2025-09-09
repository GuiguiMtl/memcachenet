using System;
using System.Buffers;
using System.Text;
using NUnit.Framework;
using memcachenet.MemCacheServer;
using memcachenet.MemCacheServer.Validations;

namespace MemCacheNetTests;

[TestFixture]
public class CommandValidatorTests
{
    [TestFixture]
    public class ValidateCommandTerminationTests
    {
        [Test]
        public void ValidateCommandTermination_ValidCRLF_ReturnsSuccess()
        {
            // Arrange
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("test command\r\n"));

            // Act
            var result = CommandValidator.ValidateCommandTermination(buffer);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateCommandTermination_MissingCRLF_ReturnsFailure()
        {
            // Arrange
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("test command"));

            // Act
            var result = CommandValidator.ValidateCommandTermination(buffer);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.ProtocolViolation));
            Assert.That(result.ErrorMessage, Is.EqualTo("command must end with \\r\\n"));
        }

        [Test]
        public void ValidateCommandTermination_OnlyLF_ReturnsFailure()
        {
            // Arrange
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("test command\n"));

            // Act
            var result = CommandValidator.ValidateCommandTermination(buffer);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.ProtocolViolation));
            Assert.That(result.ErrorMessage, Is.EqualTo("command must end with \\r\\n"));
        }

        [Test]
        public void ValidateCommandTermination_OnlyCR_ReturnsFailure()
        {
            // Arrange
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("test command\r"));

            // Act
            var result = CommandValidator.ValidateCommandTermination(buffer);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.ProtocolViolation));
            Assert.That(result.ErrorMessage, Is.EqualTo("command must end with \\r\\n"));
        }

        [Test]
        public void ValidateCommandTermination_TooShort_ReturnsFailure()
        {
            // Arrange
            var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("a"));

            // Act
            var result = CommandValidator.ValidateCommandTermination(buffer);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.ProtocolViolation));
            Assert.That(result.ErrorMessage, Is.EqualTo("command too short"));
        }

        [Test]
        public void ValidateCommandTermination_Empty_ReturnsFailure()
        {
            // Arrange
            var buffer = new ReadOnlySequence<byte>(Array.Empty<byte>());

            // Act
            var result = CommandValidator.ValidateCommandTermination(buffer);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.ProtocolViolation));
            Assert.That(result.ErrorMessage, Is.EqualTo("command too short"));
        }
    }

    [TestFixture]
    public class ValidateNumericParameterTests
    {
        [Test]
        public void ValidateNumericParameter_ValidRange_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateNumericParameter(50, "test_param", 0, 100);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateNumericParameter_AtMinBoundary_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateNumericParameter(0, "test_param", 0, 100);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateNumericParameter_AtMaxBoundary_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateNumericParameter(100, "test_param", 0, 100);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateNumericParameter_BelowMin_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateNumericParameter(-1, "test_param", 0, 100);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("test_param cannot be less than 0"));
        }

        [Test]
        public void ValidateNumericParameter_AboveMax_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateNumericParameter(101, "test_param", 0, 100);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("test_param cannot be greater than 100"));
        }

        [Test]
        public void ValidateNumericParameter_NegativeRange_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateNumericParameter(-5, "negative_param", -10, -1);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }
    }

    [TestFixture]
    public class ValidateDataLengthTests
    {
        [Test]
        public void ValidateDataLength_ValidMatch_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(100, 100, 1000);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDataLength_ZeroLength_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(0, 0, 1000);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDataLength_NegativeLength_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(-5, 0, 1000);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("data length cannot be negative"));
        }

        [Test]
        public void ValidateDataLength_ExceedsMaxSize_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(2000, 2000, 1000);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.ServerError));
            Assert.That(result.ErrorMessage, Is.EqualTo("data length exceeds maximum allowed size (1000 bytes)"));
        }

        [Test]
        public void ValidateDataLength_Mismatch_TooShort_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(100, 50, 1000);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidData));
            Assert.That(result.ErrorMessage, Is.EqualTo("data length mismatch: expected 100 bytes, got 50 bytes"));
        }

        [Test]
        public void ValidateDataLength_Mismatch_TooLong_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(50, 100, 1000);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidData));
            Assert.That(result.ErrorMessage, Is.EqualTo("data length mismatch: expected 50 bytes, got 100 bytes"));
        }

        [Test]
        public void ValidateDataLength_AtMaxSize_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateDataLength(1000, 1000, 1000);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }
    }

    [TestFixture]
    public class ValidateParameterCountTests
    {
        [Test]
        public void ValidateParameterCount_ExactMatch_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateParameterCount(5, 5, "test_command");

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateParameterCount_MoreThanExpected_ReturnsSuccess()
        {
            // Act
            var result = CommandValidator.ValidateParameterCount(7, 5, "test_command");

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateParameterCount_LessThanExpected_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateParameterCount(3, 5, "test_command");

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.MissingParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("test_command command requires 5 parameters"));
        }

        [Test]
        public void ValidateParameterCount_ZeroParameters_ReturnsFailure()
        {
            // Act
            var result = CommandValidator.ValidateParameterCount(0, 1, "get");

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.MissingParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("get command requires 1 parameters"));
        }
    }

    [TestFixture]
    public class TryParseNumericParameterTests
    {
        [Test]
        public void TryParseNumericParameter_ValidInt_ReturnsSuccessAndValue()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("123").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(value, Is.EqualTo(123));
        }

        [Test]
        public void TryParseNumericParameter_ValidUInt_ReturnsSuccessAndValue()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("4294967295").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<uint>(span, "flags", out uint value);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(value, Is.EqualTo(4294967295u));
        }

        [Test]
        public void TryParseNumericParameter_Zero_ReturnsSuccessAndValue()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("0").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(value, Is.EqualTo(0));
        }

        [Test]
        public void TryParseNumericParameter_NegativeInt_ReturnsSuccessAndValue()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("-123").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(value, Is.EqualTo(-123));
        }

        [Test]
        public void TryParseNumericParameter_InvalidFormat_ReturnsFailure()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("invalid_number").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("invalid test_param format"));
            Assert.That(value, Is.EqualTo(0)); // default value
        }

        [Test]
        public void TryParseNumericParameter_Empty_ReturnsFailure()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("invalid test_param format"));
            Assert.That(value, Is.EqualTo(0));
        }

        
        [Test]
        public void TryParseNumericParameter_Overflow_ReturnsFailure()
        {
            // Arrange - Number larger than int.MaxValue
            var span = Encoding.UTF8.GetBytes("9999999999999999999").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("invalid test_param format"));
        }

        [Test]
        public void TryParseNumericParameter_NegativeUInt_ReturnsFailure()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("-1").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<uint>(span, "flags", out uint value);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("invalid flags format"));
        }

        [Test]
        public void TryParseNumericParameter_HexFormat_ReturnsFailure()
        {
            // Arrange
            var span = Encoding.UTF8.GetBytes("0xFF").AsSpan();

            // Act
            var result = CommandValidator.TryParseNumericParameter<int>(span, "test_param", out int value);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("invalid test_param format"));
        }
    }

    [TestFixture]
    public class ValidateDataBlockFormatTests
    {
        [Test]
        public void ValidateDataBlockFormat_AlwaysReturnsSuccess()
        {
            // Arrange
            var dataBlock = Encoding.UTF8.GetBytes("test data").AsSpan();

            // Act
            var result = CommandValidator.ValidateDataBlockFormat(dataBlock);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDataBlockFormat_EmptyBlock_ReturnsSuccess()
        {
            // Arrange
            var dataBlock = ReadOnlySpan<byte>.Empty;

            // Act
            var result = CommandValidator.ValidateDataBlockFormat(dataBlock);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }
    }
}