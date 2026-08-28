// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Sqids;

namespace XiHan.Framework.DistributedIds.Tests.Sqids;

/// <summary>
/// Sqids 编码器的测试
/// </summary>
/// <remarks>
/// Sqids 的核心契约是可逆：<c>Decode(Encode(x)) == x</c>。
/// 注意编码器的字母表是在构造时按选项对象打乱一次的，因此往返必须用同一个编码器实例，
/// 跨实例的稳定性另见交付报告中的「疑似缺陷」。
/// </remarks>
public class SqidsEncoderTests
{
    /// <summary>
    /// 不传数字时返回空字符串
    /// </summary>
    [Fact]
    public void Encode_WithNoNumbers_ReturnsEmpty()
    {
        var encoder = new SqidsEncoder<int>();

        Assert.Equal(string.Empty, encoder.Encode());
    }

    /// <summary>
    /// 负数无法编码
    /// </summary>
    [Fact]
    public void Encode_WithNegativeNumber_Throws()
    {
        var encoder = new SqidsEncoder<int>();

        Assert.Throws<ArgumentException>(() => { _ = encoder.Encode(-1); });
    }

    /// <summary>
    /// 编码后再解码回到原始数字
    /// </summary>
    [Theory]
    [InlineData(226981)]
    [InlineData(1000000)]
    [InlineData(12345678)]
    [InlineData(int.MaxValue)]
    public void EncodeThenDecode_RoundTripsSingleNumber(int number)
    {
        var encoder = new SqidsEncoder<int>();

        var encoded = encoder.Encode(number);
        var decoded = encoder.Decode(encoded);

        Assert.NotEmpty(encoded);
        Assert.Equal([number], decoded);
    }

    /// <summary>
    /// 多个数字一起编码后按原顺序解码回来
    /// </summary>
    [Fact]
    public void EncodeThenDecode_RoundTripsMultipleNumbers()
    {
        var encoder = new SqidsEncoder<int>();
        int[] numbers = [1000000, 2000000, 3000000];

        var decoded = encoder.Decode(encoder.Encode(numbers));

        Assert.Equal(numbers, decoded);
    }

    /// <summary>
    /// 需要补位到最小长度的小数字同样必须可逆
    /// </summary>
    /// <remarks>
    /// 补位逻辑里 <c>id += separator + alphabet[1]</c> 是两个 char 相加（结果是 int），
    /// 拼进字符串的是十进制数字文本而不是两个字母，导致补位后的短 ID 无法解回原值。
    /// 本用例按 Sqids 的可逆语义断言，失败即为源码缺陷。
    /// </remarks>
    [Fact]
    public void EncodeThenDecode_SmallNumberNeedingPadding_RoundTrips()
    {
        var encoder = new SqidsEncoder<int>();

        var encoded = encoder.Encode(1);
        var decoded = encoder.Decode(encoded);

        Assert.Equal([1], decoded);
    }

    /// <summary>
    /// 编码结果不短于配置的最小长度
    /// </summary>
    [Fact]
    public void Encode_RespectsConfiguredMinLength()
    {
        var encoder = new SqidsEncoder<int>(new SqidsOptions
        {
            MinLength = 12
        });

        Assert.Equal(12, encoder.Encode(1).Length);
        Assert.True(encoder.Encode(int.MaxValue).Length >= 12);
    }

    /// <summary>
    /// 同一实例对同一输入的编码结果稳定
    /// </summary>
    [Fact]
    public void Encode_IsDeterministicWithinSameInstance()
    {
        var encoder = new SqidsEncoder<int>();

        Assert.Equal(encoder.Encode(12345678), encoder.Encode(12345678));
    }

    /// <summary>
    /// 编码结果只使用字母表内的字符
    /// </summary>
    [Fact]
    public void Encode_UsesOnlyAlphabetCharacters()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        var encoder = new SqidsEncoder<int>(new SqidsOptions
        {
            Alphabet = alphabet,
            MinLength = 3
        });

        var encoded = encoder.Encode(12345678);

        Assert.All(encoded, character => Assert.True(alphabet.Contains(character), $"字符 {character} 不在字母表内"));
    }

    /// <summary>
    /// 自定义字母表下同样可逆
    /// </summary>
    [Fact]
    public void EncodeThenDecode_WithCustomAlphabet_RoundTrips()
    {
        var encoder = new SqidsEncoder<int>(new SqidsOptions
        {
            Alphabet = "abcdefghijklmnopqrstuvwxyz",
            MinLength = 3
        });

        Assert.Equal([12345678], encoder.Decode(encoder.Encode(12345678)));
    }

    /// <summary>
    /// 字母表少于 3 个字符时拒绝构造
    /// </summary>
    [Fact]
    public void Constructor_WithTooShortAlphabet_Throws()
    {
        var options = new SqidsOptions
        {
            Alphabet = "ab"
        };

        Assert.Throws<ArgumentException>(() => { _ = new SqidsEncoder<int>(options); });
    }

    /// <summary>
    /// 字母表含重复字符时拒绝构造
    /// </summary>
    [Fact]
    public void Constructor_WithDuplicatedAlphabet_Throws()
    {
        var options = new SqidsOptions
        {
            Alphabet = "aab"
        };

        Assert.Throws<ArgumentException>(() => { _ = new SqidsEncoder<int>(options); });
    }

    /// <summary>
    /// 解码空串返回空数组
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Decode_WithNullOrEmpty_ReturnsEmpty(string? id)
    {
        var encoder = new SqidsEncoder<int>();

        Assert.Empty(encoder.Decode(id!));
    }

    /// <summary>
    /// 解码含字母表外字符的串返回空数组，而不是抛异常
    /// </summary>
    [Fact]
    public void Decode_WithCharactersOutsideAlphabet_ReturnsEmpty()
    {
        var encoder = new SqidsEncoder<int>();

        Assert.Empty(encoder.Decode("!!!"));
        Assert.Empty(encoder.Decode("a-b-c"));
    }

    /// <summary>
    /// 长整型编码器同样可逆
    /// </summary>
    [Fact]
    public void LongEncoder_RoundTripsLargeValue()
    {
        var encoder = new SqidsEncoder<long>();
        const long number = 1_000_000_000_000L;

        Assert.Equal([number], encoder.Decode(encoder.Encode(number)));
    }

    /// <summary>
    /// 非泛型包装器沿用同一套编解码语义
    /// </summary>
    [Fact]
    public void NonGenericEncoder_RoundTripsInt32()
    {
        var encoder = new SqidsEncoder();

        var encoded = encoder.Encode(12345678);

        Assert.NotEmpty(encoded);
        Assert.Equal([12345678], encoder.Decode(encoded));
    }
}
