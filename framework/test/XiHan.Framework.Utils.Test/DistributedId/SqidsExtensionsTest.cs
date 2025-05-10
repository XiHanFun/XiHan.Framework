using JetBrains.Annotations;
using XiHan.Framework.Utils.DistributedId.Sqids;

namespace XiHan.Framework.Utils.Test.DistributedId;

/// <summary>
/// Sqids扩展方法测试
/// </summary>
[TestSubject(typeof(SqidsExtensions))]
public class SqidsExtensionsTest
{
    [Fact(DisplayName = "Int32转Sqid测试")]
    public void ToSqid_FromInt32_ShouldWork()
    {
        // Arrange
        var number = 12345;

        // Act
        var sqid = number.ToSqid();

        // Assert
        Assert.NotEmpty(sqid);
    }

    [Fact(DisplayName = "Int32数组转Sqid测试")]
    public void ToSqid_FromInt32Array_ShouldWork()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Act
        var sqid = numbers.ToSqid();

        // Assert
        Assert.NotEmpty(sqid);
    }

    [Fact(DisplayName = "Int64转Sqid测试")]
    public void ToSqid_FromInt64_ShouldWork()
    {
        // Arrange
        var number = 9223372036854775807L; // long.MaxValue

        // Act
        var sqid = number.ToSqid();

        // Assert
        Assert.NotEmpty(sqid);
    }

    [Fact(DisplayName = "Int64数组转Sqid测试")]
    public void ToSqid_FromInt64Array_ShouldWork()
    {
        // Arrange
        var numbers = new[] { 1000000000L, 2000000000L, 3000000000L };

        // Act
        var sqid = numbers.ToSqid();

        // Assert
        Assert.NotEmpty(sqid);
    }

    [Fact(DisplayName = "UInt32转Sqid测试")]
    public void ToSqid_FromUInt32_ShouldWork()
    {
        // Arrange
        var number = 4294967295; // uint.MaxValue

        // Act
        var sqid = number.ToSqid();

        // Assert
        Assert.NotEmpty(sqid);
    }

    [Fact(DisplayName = "UInt64转Sqid测试")]
    public void ToSqid_FromUInt64_ShouldWork()
    {
        // Arrange
        var number = 18446744073709551615UL; // ulong.MaxValue

        // Act
        var sqid = number.ToSqid();

        // Assert
        Assert.NotEmpty(sqid);
    }

    [Fact(DisplayName = "Sqid转Int32测试")]
    public void FromSqidToInt32_ShouldWork()
    {
        // Arrange
        var number = 12345;
        var sqid = number.ToSqid();

        // Act
        var result = sqid.FromSqidToInt32();

        // Assert
        Assert.Equal(number, result);
    }

    [Fact(DisplayName = "Sqid转Int32数组测试")]
    public void FromSqidToInt32Array_ShouldWork()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var sqid = numbers.ToSqid();

        // Act
        var result = sqid.FromSqidToInt32Array();

        // Assert
        Assert.Equal(numbers.Length, result.Length);
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.Equal(numbers[i], result[i]);
        }
    }

    [Fact(DisplayName = "Sqid转Int64测试")]
    public void FromSqidToInt64_ShouldWork()
    {
        // Arrange
        var number = 9223372036854775807L; // long.MaxValue
        var sqid = number.ToSqid();

        // Act
        var result = sqid.FromSqidToInt64();

        // Assert
        Assert.Equal(number, result);
    }

    [Fact(DisplayName = "Sqid转Int64数组测试")]
    public void FromSqidToInt64Array_ShouldWork()
    {
        // Arrange
        var numbers = new[] { 1000000000L, 2000000000L, 3000000000L };
        var sqid = numbers.ToSqid();

        // Act
        var result = sqid.FromSqidToInt64Array();

        // Assert
        Assert.Equal(numbers.Length, result.Length);
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.Equal(numbers[i], result[i]);
        }
    }

    [Fact(DisplayName = "Sqid转UInt32测试")]
    public void FromSqidToUInt32_ShouldWork()
    {
        // Arrange
        var number = 4294967295; // uint.MaxValue
        var sqid = number.ToSqid();

        // Act
        var result = sqid.FromSqidToUInt32();

        // Assert
        Assert.Equal(number, result);
    }

    [Fact(DisplayName = "Sqid转UInt64测试")]
    public void FromSqidToUInt64_ShouldWork()
    {
        // Arrange
        var number = 18446744073709551615UL; // ulong.MaxValue
        var sqid = number.ToSqid();

        // Act
        var result = sqid.FromSqidToUInt64();

        // Assert
        Assert.Equal(number, result);
    }

    [Fact(DisplayName = "空字符串解码测试")]
    public void FromEmptySqid_ShouldReturnZeroOrEmpty()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act
        var int32Result = emptyString.FromSqidToInt32();
        var int32ArrayResult = emptyString.FromSqidToInt32Array();
        var int64Result = emptyString.FromSqidToInt64();
        var int64ArrayResult = emptyString.FromSqidToInt64Array();
        var uint32Result = emptyString.FromSqidToUInt32();
        var uint64Result = emptyString.FromSqidToUInt64();

        // Assert
        Assert.Equal(0, int32Result);
        Assert.Empty(int32ArrayResult);
        Assert.Equal(0L, int64Result);
        Assert.Empty(int64ArrayResult);
        Assert.Equal(0U, uint32Result);
        Assert.Equal(0UL, uint64Result);
    }

    [Fact(DisplayName = "无效字符串解码测试")]
    public void FromInvalidSqid_ShouldReturnZeroOrEmpty()
    {
        // Arrange
        var invalidString = "这不是有效的Sqids字符串";

        // Act
        var int32Result = invalidString.FromSqidToInt32();
        var int32ArrayResult = invalidString.FromSqidToInt32Array();
        var int64Result = invalidString.FromSqidToInt64();
        var int64ArrayResult = invalidString.FromSqidToInt64Array();
        var uint32Result = invalidString.FromSqidToUInt32();
        var uint64Result = invalidString.FromSqidToUInt64();

        // Assert
        Assert.Equal(0, int32Result);
        Assert.Empty(int32ArrayResult);
        Assert.Equal(0L, int64Result);
        Assert.Empty(int64ArrayResult);
        Assert.Equal(0U, uint32Result);
        Assert.Equal(0UL, uint64Result);
    }

    [Fact(DisplayName = "循环编解码测试")]
    public void RoundTrip_ShouldPreserveValues()
    {
        // Arrange
        var int32 = 12345;
        var int64 = 9223372036854775807L;
        var uint32 = 4294967295U;
        var uint64 = 18446744073709551615UL;
        var int32Array = new[] { 1, 2, 3, 4, 5 };
        var int64Array = new[] { 1000000000L, 2000000000L, 3000000000L };

        // Act & Assert
        Assert.Equal(int32, int32.ToSqid().FromSqidToInt32());
        Assert.Equal(int64, int64.ToSqid().FromSqidToInt64());
        Assert.Equal(uint32, uint32.ToSqid().FromSqidToUInt32());
        Assert.Equal(uint64, uint64.ToSqid().FromSqidToUInt64());

        var decodedInt32Array = int32Array.ToSqid().FromSqidToInt32Array();
        Assert.Equal(int32Array.Length, decodedInt32Array.Length);
        for (var i = 0; i < int32Array.Length; i++)
        {
            Assert.Equal(int32Array[i], decodedInt32Array[i]);
        }

        var decodedInt64Array = int64Array.ToSqid().FromSqidToInt64Array();
        Assert.Equal(int64Array.Length, decodedInt64Array.Length);
        for (var i = 0; i < int64Array.Length; i++)
        {
            Assert.Equal(int64Array[i], decodedInt64Array[i]);
        }
    }
}
