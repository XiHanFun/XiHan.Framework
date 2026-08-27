// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Sqids;

namespace XiHan.Framework.DistributedIds.Tests.Sqids;

/// <summary>
/// Sqids 扩展方法的测试
/// </summary>
/// <remarks>
/// 扩展方法内部按数字类型各持有一个静态编码器，所以「同类型编码 + 同类型解码」才构成往返；
/// 用例取的都是够大的数（编码后自然长度已达到最小长度），避开补位路径，专测类型分派与往返本身。
/// </remarks>
public class SqidsExtensionsTests
{
    /// <summary>
    /// 整数编码后可以解回原值
    /// </summary>
    [Fact]
    public void Int32_RoundTrips()
    {
        const int number = 12345678;

        var sqid = number.ToSqid();

        Assert.NotEmpty(sqid);
        Assert.Equal(number, sqid.FromSqidToInt32());
    }

    /// <summary>
    /// 长整数编码后可以解回原值
    /// </summary>
    [Fact]
    public void Int64_RoundTrips()
    {
        const long number = 1234567890123L;

        var sqid = number.ToSqid();

        Assert.NotEmpty(sqid);
        Assert.Equal(number, sqid.FromSqidToInt64());
    }

    /// <summary>
    /// 无符号整数编码后可以解回原值
    /// </summary>
    [Fact]
    public void UInt32_RoundTrips()
    {
        const uint number = 3000000000u;

        var sqid = number.ToSqid();

        Assert.NotEmpty(sqid);
        Assert.Equal(number, sqid.FromSqidToUInt32());
    }

    /// <summary>
    /// 无符号长整数编码后可以解回原值
    /// </summary>
    [Fact]
    public void UInt64_RoundTrips()
    {
        const ulong number = 9876543210UL;

        var sqid = number.ToSqid();

        Assert.NotEmpty(sqid);
        Assert.Equal(number, sqid.FromSqidToUInt64());
    }

    /// <summary>
    /// 整数数组编码后按原顺序解回
    /// </summary>
    [Fact]
    public void Int32Array_RoundTripsInOrder()
    {
        int[] numbers = [1000000, 2000000, 3000000];

        var decoded = numbers.ToSqid().FromSqidToInt32Array();

        Assert.Equal(numbers, decoded);
    }

    /// <summary>
    /// 长整数数组编码后按原顺序解回
    /// </summary>
    [Fact]
    public void Int64Array_RoundTripsInOrder()
    {
        long[] numbers = [10000000000L, 20000000000L];

        var decoded = numbers.ToSqid().FromSqidToInt64Array();

        Assert.Equal(numbers, decoded);
    }

    /// <summary>
    /// 不同数值编码出不同的短串
    /// </summary>
    [Fact]
    public void ToSqid_DifferentNumbers_ProduceDifferentText()
    {
        Assert.NotEqual(12345678.ToSqid(), 12345679.ToSqid());
    }

    /// <summary>
    /// 解码空串或非法串时回落到 0，而不是抛异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    public void FromSqid_WithEmptyOrInvalidText_FallsBackToZero(string text)
    {
        Assert.Equal(0, text.FromSqidToInt32());
        Assert.Equal(0L, text.FromSqidToInt64());
        Assert.Equal(0u, text.FromSqidToUInt32());
        Assert.Equal(0UL, text.FromSqidToUInt64());
    }

    /// <summary>
    /// 解码空串或非法串时数组形式返回空数组
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    public void FromSqidToArray_WithEmptyOrInvalidText_ReturnsEmpty(string text)
    {
        Assert.Empty(text.FromSqidToInt32Array());
        Assert.Empty(text.FromSqidToInt64Array());
    }
}
