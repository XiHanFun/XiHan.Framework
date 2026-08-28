// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// 自定义进制编码器的字节序与缓冲区容量回归测试
/// </summary>
/// <remarks>
/// 原实现两处缺陷：
/// 一是 Encode 按小端读入、Decode 按大端写回，多字节往返后字节序颠倒；
/// 二是缓冲区容量公式写成 Math.Log(_alphabet.Length, 256)（即 log_256(基数)，恒小于 1），
/// 底数与真数写反了，正确的每字节位数是 log_基数(256)：
/// 二元字符集连 1 个字节都放不下，36 字符集的 3 字节只给 4 位而实际需要 5 位，
/// 都会在 resultSpan[index++] 处抛 IndexOutOfRangeException。
/// </remarks>
public class CustomRadixByteOrderTests
{
    private const string Base36Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 二元字符集下 1 字节需要 8 位，原容量公式只给 1 位必然越界
    /// </summary>
    [Fact]
    public void Encode_WithBinaryAlphabet_DoesNotOverflowBuffer()
    {
        var radix = new CustomRadix("01");

        Assert.Equal("11111111", radix.Encode([0xFF]));
    }

    /// <summary>
    /// 二元字符集下的往返恒等
    /// </summary>
    [Theory]
    [InlineData("ff")]
    [InlineData("0102")]
    [InlineData("00ff")]
    public void EncodeAndDecode_WithBinaryAlphabet_RoundTrip(string hex)
    {
        var radix = new CustomRadix("01");
        var data = Convert.FromHexString(hex);

        Assert.Equal(data, radix.Decode(radix.Encode(data)));
    }

    /// <summary>
    /// 36 字符集下 3 字节需要 5 位，原容量公式只给 4 位必然越界
    /// </summary>
    [Fact]
    public void Encode_WithBase36Alphabet_ThreeBytes_DoesNotOverflowBuffer()
    {
        var radix = new CustomRadix(Base36Alphabet);

        var encoded = radix.Encode([0xFF, 0xFF, 0xFF]);

        Assert.Equal(5, encoded.Length);
    }

    /// <summary>
    /// 多字节按大端解释，与同字符集的 Base36 结果一致
    /// </summary>
    [Fact]
    public void Encode_MultiByte_UsesBigEndianOrderAndMatchesBase36()
    {
        var radix = new CustomRadix(Base36Alphabet);

        Assert.Equal("76", radix.Encode([0x01, 0x02]));
        Assert.Equal(Base36.Encode([0xDE, 0xAD, 0xBE, 0xEF]), radix.Encode([0xDE, 0xAD, 0xBE, 0xEF]));
    }

    /// <summary>
    /// 多字节往返恒等（含前导零与最大值）
    /// </summary>
    [Theory]
    [InlineData("0102")]
    [InlineData("010203")]
    [InlineData("0001")]
    [InlineData("ffff")]
    [InlineData("ffffff")]
    [InlineData("deadbeef")]
    public void EncodeAndDecode_WithBase36Alphabet_RoundTrip(string hex)
    {
        var radix = new CustomRadix(Base36Alphabet);
        var data = Convert.FromHexString(hex);

        Assert.Equal(data, radix.Decode(radix.Encode(data)));
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常，而不是空引用异常
    /// </summary>
    [Fact]
    public void Encode_WhenDataIsNull_ThrowsArgumentNull()
    {
        var radix = new CustomRadix(Base36Alphabet);

        Assert.Throws<ArgumentNullException>(() => radix.Encode(null!));
    }

    /// <summary>
    /// 解码入参为 null 时抛参数空异常，而不是空引用异常
    /// </summary>
    [Fact]
    public void Decode_WhenEncodedIsNull_ThrowsArgumentNull()
    {
        var radix = new CustomRadix(Base36Alphabet);

        Assert.Throws<ArgumentNullException>(() => radix.Decode(null!));
    }
}
