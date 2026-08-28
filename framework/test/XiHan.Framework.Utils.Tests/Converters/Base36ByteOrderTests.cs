// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// Base36 多字节字节序与缓冲区容量回归测试
/// </summary>
/// <remarks>
/// 原实现 Encode 侧按小端读入（补零缓冲 + new BigInteger(span)），Decode 侧按大端写回，
/// 两端字节序不一致，多字节输入往返后整体颠倒；
/// 同时结果缓冲区按 1.4 倍估算，而 log36(256) ≈ 1.5475，2 字节的 0xFFFF 就会越界抛异常。
/// 本文件锁「大端往返」与「容量足够」，并补上与同组 Base32 对齐的空值契约。
/// </remarks>
public class Base36ByteOrderTests
{
    /// <summary>
    /// 多字节按大端解释：{0x01,0x02} 即 258，Base36 记作 76
    /// </summary>
    [Fact]
    public void Encode_MultiByte_UsesBigEndianOrder()
    {
        Assert.Equal("76", Base36.Encode([0x01, 0x02]));
    }

    /// <summary>
    /// 解码结果与编码入参字节序一致，不再颠倒
    /// </summary>
    [Fact]
    public void Decode_MultiByte_KeepsBigEndianOrder()
    {
        Assert.Equal(new byte[] { 0x01, 0x02 }, Base36.Decode("76"));
    }

    /// <summary>
    /// 多字节往返恒等（含前导零、最大值、奇数长度）
    /// </summary>
    [Theory]
    [InlineData("0102")]
    [InlineData("010203")]
    [InlineData("0001")]
    [InlineData("000102")]
    [InlineData("ffff")]
    [InlineData("ffffff")]
    [InlineData("deadbeef")]
    [InlineData("8000")]
    [InlineData("0080")]
    public void EncodeAndDecode_MultiByte_RoundTrip(string hex)
    {
        var data = Convert.FromHexString(hex);

        var roundTripped = Base36.Decode(Base36.Encode(data));

        Assert.Equal(data, roundTripped);
    }

    /// <summary>
    /// 两字节最大值需要 4 位，原 1.4 倍容量只给 3 位会越界
    /// </summary>
    [Fact]
    public void Encode_TwoByteMaxValue_DoesNotOverflowBuffer()
    {
        Assert.Equal("1EKF", Base36.Encode([0xFF, 0xFF]));
    }

    /// <summary>
    /// 三字节最大值同样不越界
    /// </summary>
    [Fact]
    public void Encode_ThreeByteMaxValue_DoesNotOverflowBuffer()
    {
        var encoded = Base36.Encode([0xFF, 0xFF, 0xFF]);

        Assert.Equal(5, encoded.Length);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF }, Base36.Decode(encoded));
    }

    /// <summary>
    /// 前导零字节会编码成前导零字符，并在解码时还原
    /// </summary>
    [Fact]
    public void Encode_LeadingZeroBytes_AreEncodedAsLeadingZeroChars()
    {
        Assert.Equal("01", Base36.Encode([0x00, 0x01]));
        Assert.Equal(new byte[] { 0x00, 0x01 }, Base36.Decode("01"));
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常，而不是空引用异常
    /// </summary>
    [Fact]
    public void Encode_WhenDataIsNull_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Base36.Encode(null!));
    }

    /// <summary>
    /// 解码入参为 null 时抛参数空异常，而不是空引用异常
    /// </summary>
    [Fact]
    public void Decode_WhenEncodedIsNull_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Base36.Decode(null!));
    }
}
