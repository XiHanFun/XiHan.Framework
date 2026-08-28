// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// Base62 编解码测试
/// </summary>
/// <remarks>
/// 该实现的编码端与解码端字节序一致，因此多字节往返是成立的，这里正面覆盖。
/// 末位为 0x00 的输入属于数值编码的固有信息丢失（高位零不可还原），不纳入往返用例。
/// </remarks>
public class Base62Tests
{
    /// <summary>
    /// 字母表按 0-9A-Za-z 顺序映射
    /// </summary>
    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1L, "1")]
    [InlineData(9L, "9")]
    [InlineData(10L, "A")]
    [InlineData(35L, "Z")]
    [InlineData(36L, "a")]
    [InlineData(61L, "z")]
    [InlineData(62L, "10")]
    public void EncodeLong_UsesDigitsUpperThenLower(long value, string expected)
    {
        Assert.Equal(expected, Base62.EncodeLong(value));
    }

    /// <summary>
    /// 长整数编解码可往返
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(61L)]
    [InlineData(62L)]
    [InlineData(1234567890L)]
    [InlineData(long.MaxValue)]
    public void EncodeLongAndDecodeLong_RoundTrip(long value)
    {
        Assert.Equal(value, Base62.DecodeLong(Base62.EncodeLong(value)));
    }

    /// <summary>
    /// 空串解码为零
    /// </summary>
    [Fact]
    public void DecodeLong_WithEmptyString_ReturnsZero()
    {
        Assert.Equal(0L, Base62.DecodeLong(string.Empty));
    }

    /// <summary>
    /// 字节数组编解码可往返
    /// </summary>
    [Fact]
    public void EncodeAndDecode_RoundTripBytes()
    {
        byte[][] samples =
        [
            [],
            [0x01],
            [0xFF],
            [0x01, 0x02, 0x03],
            [0x00, 0x01],
            [0x10, 0x20, 0x30],
            [0xDE, 0xAD, 0xBE, 0xEF]
        ];

        foreach (var sample in samples)
        {
            Assert.Equal(sample, Base62.Decode(Base62.Encode(sample)));
        }
    }

    /// <summary>
    /// 空数组编码为单个零字符
    /// </summary>
    [Fact]
    public void Encode_WithEmptyArray_ReturnsZeroDigit()
    {
        Assert.Equal("0", Base62.Encode([]));
    }

    /// <summary>
    /// 出现字母表以外的字符时抛键不存在异常
    /// </summary>
    [Fact]
    public void DecodeAndDecodeLong_WhenCharacterIsIllegal_Throw()
    {
        Assert.Throws<KeyNotFoundException>(() => Base62.Decode("-"));
        Assert.Throws<KeyNotFoundException>(() => Base62.DecodeLong("曦"));
    }
}
