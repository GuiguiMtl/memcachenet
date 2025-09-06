using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using memcachenet.MemCacheServer;

namespace MemCacheNetTests;

[TestFixture]
public class KeyValidatorTests
{
    [TestFixture]
    public class ValidateKeyTests
    {
        private const int MaxKeySize = 250;

        [Test]
        public void ValidateKey_ValidKey_ReturnsSuccess()
        {
            // Act
            var result = KeyValidator.ValidateKey("valid_key", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_ValidKeyWithNumbers_ReturnsSuccess()
        {
            // Act
            var result = KeyValidator.ValidateKey("key123", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_ValidKeyWithSpecialChars_ReturnsSuccess()
        {
            // Act
            var result = KeyValidator.ValidateKey("key-with_dots.and-dashes", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_MixedCaseKey_ReturnsSuccess()
        {
            // Act
            var result = KeyValidator.ValidateKey("MixedCaseKey", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_SingleCharacter_ReturnsSuccess()
        {
            // Act
            var result = KeyValidator.ValidateKey("a", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_MaxLengthKey_ReturnsSuccess()
        {
            // Arrange
            var key = new string('k', MaxKeySize);

            // Act
            var result = KeyValidator.ValidateKey(key, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_UnicodeKey_ReturnsSuccess()
        {
            // Act
            var result = KeyValidator.ValidateKey("café", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_NullKey_ReturnsFailure()
        {
            // Act
            var result = KeyValidator.ValidateKey(null, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key cannot be empty"));
        }

        [Test]
        public void ValidateKey_EmptyKey_ReturnsFailure()
        {
            // Act
            var result = KeyValidator.ValidateKey("", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key cannot be empty"));
        }

        [Test]
        public void ValidateKey_WhitespaceOnlyKey_ReturnsFailure()
        {
            // Act
            var result = KeyValidator.ValidateKey("   ", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key cannot be whitespace only"));
        }

        [Test]
        public void ValidateKey_TabOnlyKey_ReturnsFailure()
        {
            // Act
            var result = KeyValidator.ValidateKey("\t", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key cannot be whitespace only"));
        }

        [Test]
        public void ValidateKey_TooLongKey_ReturnsFailure()
        {
            // Arrange
            var key = new string('k', MaxKeySize + 1);

            // Act
            var result = KeyValidator.ValidateKey(key, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo($"key too long (max {MaxKeySize} bytes)"));
        }

        [Test]
        public void ValidateKey_UnicodeKeyTooLong_ReturnsFailure()
        {
            // Arrange - Unicode characters take multiple bytes in UTF-8
            var key = new string('é', 200); // 'é' is 2 bytes in UTF-8, so 400 bytes total

            // Act
            var result = KeyValidator.ValidateKey(key, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo($"key too long (max {MaxKeySize} bytes)"));
        }

        [Test]
        public void ValidateKey_ContainsSpace_ReturnsFailure()
        {
            // Act
            var result = KeyValidator.ValidateKey("key with space", MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key contains spaces"));
        }

        [TestCase('\0', TestName = "NullCharacter")]
        [TestCase('\x01', TestName = "StartOfHeading")]
        [TestCase('\x08', TestName = "Backspace")]
        [TestCase('\t', TestName = "Tab")]
        [TestCase('\n', TestName = "LineFeed")]
        [TestCase('\r', TestName = "CarriageReturn")]
        [TestCase('\x1F', TestName = "UnitSeparator")]
        [TestCase('\x7F', TestName = "Delete")]
        public void ValidateKey_ContainsControlCharacters_ReturnsFailure(char controlChar)
        {
            // Arrange
            var key = $"key{controlChar}test";

            // Act
            var result = KeyValidator.ValidateKey(key, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key contains control characters"));
        }

        [Test]
        public void ValidateKey_ValidSpecialCharacters_ReturnsSuccess()
        {
            // Arrange - Test various printable special characters that should be allowed
            var key = "!@#$%^&*()[]{}|;':\",./<>?`~+=_-";

            // Act
            var result = KeyValidator.ValidateKey(key, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKey_ZeroMaxSize_ReturnsFailure()
        {
            // Act
            var result = KeyValidator.ValidateKey("a", 0);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key too long (max 0 bytes)"));
        }
    }

    [TestFixture]
    public class ValidateKeysTests
    {
        private const int MaxKeySize = 250;

        [Test]
        public void ValidateKeys_ValidSingleKey_ReturnsSuccess()
        {
            // Arrange
            var keys = new[] { "valid_key" };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKeys_ValidMultipleKeys_ReturnsSuccess()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3", "key_with_underscore" };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKeys_EmptyCollection_ReturnsFailure()
        {
            // Arrange
            var keys = Array.Empty<string>();

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.MissingParameter));
            Assert.That(result.ErrorMessage, Is.EqualTo("no keys provided"));
        }

        [Test]
        public void ValidateKeys_FirstKeyInvalid_ReturnsFirstFailure()
        {
            // Arrange
            var keys = new[] { "", "valid_key" };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key cannot be empty"));
        }

        [Test]
        public void ValidateKeys_SecondKeyInvalid_ReturnsFailureForSecondKey()
        {
            // Arrange
            var keys = new[] { "valid_key", "key with space" };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key contains spaces"));
        }

        [Test]
        public void ValidateKeys_LastKeyInvalid_ReturnsFailureForLastKey()
        {
            // Arrange
            var keys = new[] { "key1", "key2", new string('k', MaxKeySize + 1) };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo($"key too long (max {MaxKeySize} bytes)"));
        }

        [Test]
        public void ValidateKeys_ManyValidKeys_ReturnsSuccess()
        {
            // Arrange
            var keys = Enumerable.Range(1, 100).Select(i => $"key{i}").ToArray();

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKeys_DuplicateKeys_ReturnsSuccess()
        {
            // Arrange - Duplicate keys should be allowed at validation level
            var keys = new[] { "key1", "key1", "key2" };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateKeys_NullInCollection_ReturnsFailure()
        {
            // Arrange
            var keys = new[] { "valid_key", null!, "another_key" };

            // Act
            var result = KeyValidator.ValidateKeys(keys, MaxKeySize);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo(ValidationErrorType.InvalidKey));
            Assert.That(result.ErrorMessage, Is.EqualTo("key cannot be empty"));
        }
    }
}