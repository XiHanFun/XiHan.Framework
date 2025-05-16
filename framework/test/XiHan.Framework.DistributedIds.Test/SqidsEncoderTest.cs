using JetBrains.Annotations;
using XiHan.Framework.DistributedIds.Sqids;
using XiHan.Framework.Utils.Maths;

namespace XiHan.Framework.DistributedIds.Test;

/// <summary>
/// Sqids编码器测试
/// </summary>
[TestSubject(typeof(SqidsEncoder<>))]
public class SqidsEncoderTest
{
    private readonly SqidsEncoder _defaultEncoder;
    private readonly SqidsEncoder<int> _intEncoder;
    private readonly SqidsEncoder<long> _longEncoder;
    private readonly SqidsOptions _customOptions;
    private readonly SqidsEncoder _customEncoder;

    public SqidsEncoderTest()
    {
        _defaultEncoder = new SqidsEncoder();
        _intEncoder = new SqidsEncoder<int>();
        _longEncoder = new SqidsEncoder<long>();

        _customOptions = new SqidsOptions
        {
            Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890",
            MinLength = 10
        };
        _customEncoder = new SqidsEncoder(_customOptions);
    }

    [Fact(DisplayName = "默认编码器解码测试")]
    public void Decode_WithDefaultEncoder_ShouldDecodeCorrectly()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3 };
        var encoded = _defaultEncoder.Encode(numbers);

        // Act
        var decoded = _defaultEncoder.Decode(encoded);

        // Assert
        Assert.Equal(numbers.Length, decoded.Length);
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.Equal(numbers[i], decoded[i]);
        }
    }

    [Fact(DisplayName = "自定义编码器测试")]
    public void Encode_WithCustomOptions_ShouldRespectOptions()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3 };

        // Act
        var result = _customEncoder.Encode(numbers);

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Length >= _customOptions.MinLength);
        Assert.All(result, c => _customOptions.Alphabet.Contains(c));
    }

    [Fact(DisplayName = "Int32编码器测试")]
    public void Int32Encoder_ShouldWorkCorrectly()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3 };

        // Act
        var encoded = _intEncoder.Encode(numbers);
        var decoded = _intEncoder.Decode(encoded);

        // Assert
        Assert.Equal(numbers.Length, decoded.Length);
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.Equal(numbers[i], decoded[i]);
        }
    }

    [Fact(DisplayName = "Int64编码器测试")]
    public void Int64Encoder_ShouldWorkCorrectly()
    {
        // Arrange
        var numbers = new[] { 1000000000L, 2000000000L, 3000000000L };

        // Act
        var encoded = _longEncoder.Encode(numbers);
        var decoded = _longEncoder.Decode(encoded);

        // Assert
        Assert.Equal(numbers.Length, decoded.Length);
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.Equal(numbers[i], decoded[i]);
        }
    }

    [Fact(DisplayName = "空输入测试")]
    public void Encode_WithEmptyInput_ShouldReturnEmptyString()
    {
        // Arrange
        var emptyArray = Array.Empty<int>();

        // Act
        var result = _defaultEncoder.Encode(emptyArray);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "空字符串解码测试")]
    public void Decode_WithEmptyInput_ShouldReturnEmptyArray()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act
        var result = _defaultEncoder.Decode(emptyString);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "单个数字编解码测试")]
    public void EncodeDecode_WithSingleNumber_ShouldWork()
    {
        // Arrange
        var number = 12345;

        // Act
        var encoded = _defaultEncoder.Encode(number);
        var decoded = _defaultEncoder.Decode(encoded);

        // Assert
        Assert.Single(decoded);
        Assert.Equal(number, decoded[0]);
    }

    [Fact(DisplayName = "大数字测试")]
    public void EncodeDecode_WithLargeNumbers_ShouldWork()
    {
        // Arrange
        var numbers = new[] { int.MaxValue - 1, int.MaxValue };

        // Act
        var encoded = _defaultEncoder.Encode(numbers);
        var decoded = _defaultEncoder.Decode(encoded);

        // Assert
        Assert.Equal(numbers.Length, decoded.Length);
        Assert.Equal(numbers[0], decoded[0]);
        Assert.Equal(numbers[1], decoded[1]);
    }

    [Fact(DisplayName = "无效字符解码测试")]
    public void Decode_WithInvalidCharacters_ShouldReturnEmptyArray()
    {
        // Arrange
        var invalidId = "这不是有效的Sqids字符串";

        // Act
        var result = _defaultEncoder.Decode(invalidId);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "屏蔽词测试")]
    public void Encode_WithBlocklistedResult_ShouldAvoidBlocklist()
    {
        // Arrange
        var options = new SqidsOptions();
        var encoder = new SqidsEncoder(options);
        var testWord = options.BlockList.First();

        // 尝试加入一些数字并检查结果
        for (var i = 0; i < 1000; i += 100)
        {
            // Act
            var encoded = encoder.Encode(i, i + 1, i + 2);

            // Assert
            var lowerEncoded = encoded.ToLowerInvariant();
            Assert.DoesNotContain(testWord, lowerEncoded);
        }
    }

    [Fact(DisplayName = "选项验证测试")]
    public void Constructor_WithInvalidOptions_ShouldThrowException()
    {
        // Arrange
        var tooShortAlphabet = new SqidsOptions { Alphabet = "ab" };
        var duplicateChars = new SqidsOptions { Alphabet = "aabcdef" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SqidsEncoder(tooShortAlphabet));
        Assert.Throws<ArgumentException>(() => new SqidsEncoder(duplicateChars));
    }
}
