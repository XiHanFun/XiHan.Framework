// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.NanoIds;

namespace XiHan.Framework.DistributedIds.Tests.NanoIds;

/// <summary>
/// NanoID 生成器配置选项的测试
/// </summary>
/// <remarks>
/// 字符集常量直接决定已落库 ID 的字符空间，改动会让历史 ID 的校验规则失效，因此逐个锁死；
/// 长度与字符集的合法性校验是防止「碰撞概率被悄悄放大」的唯一闸门，必须覆盖越界路径。
/// </remarks>
public class NanoIdOptionsTests
{
    /// <summary>
    /// 配置节名称被 appsettings 直接引用，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:DistributedIds:NanoId", NanoIdOptions.SectionName);
    }

    /// <summary>
    /// 内置字符集常量的字面值被历史 ID 依赖，不允许漂移
    /// </summary>
    [Fact]
    public void AlphabetConstants_AreStable()
    {
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", NanoIdOptions.DefaultAlphabet);
        Assert.Equal("0123456789", NanoIdOptions.NumbersAlphabet);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", NanoIdOptions.LowercaseAlphabet);
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ", NanoIdOptions.UppercaseAlphabet);
        Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", NanoIdOptions.UrlSafeAlphabet);
        Assert.Equal("0123456789abcdef", NanoIdOptions.HexAlphabet);
    }

    /// <summary>
    /// 所有内置字符集内部不含重复字符
    /// </summary>
    [Theory]
    [InlineData(NanoIdOptions.DefaultAlphabet, 62)]
    [InlineData(NanoIdOptions.NumbersAlphabet, 10)]
    [InlineData(NanoIdOptions.LowercaseAlphabet, 26)]
    [InlineData(NanoIdOptions.UppercaseAlphabet, 26)]
    [InlineData(NanoIdOptions.UrlSafeAlphabet, 64)]
    [InlineData(NanoIdOptions.SafeAlphabet, 48)]
    [InlineData(NanoIdOptions.HexAlphabet, 16)]
    public void AlphabetConstants_HaveDistinctCharactersAndExpectedSize(string alphabet, int expectedLength)
    {
        Assert.Equal(expectedLength, alphabet.Length);
        Assert.Equal(alphabet.Length, alphabet.Distinct().Count());
    }

    /// <summary>
    /// 防混淆字符集里不出现形近字符
    /// </summary>
    [Fact]
    public void SafeAlphabet_ExcludesLookAlikeCharacters()
    {
        foreach (var lookAlike in "01lIoO2Z5sSuv")
        {
            Assert.False(NanoIdOptions.SafeAlphabet.Contains(lookAlike), $"防混淆字符集不应包含 {lookAlike}");
        }
    }

    /// <summary>
    /// 新建选项时各字段落在文档描述的默认值上
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var options = new NanoIdOptions();

        Assert.Equal(21, options.Size);
        Assert.Equal(NanoIdOptions.DefaultAlphabet, options.Alphabet);
        Assert.Equal(NanoIdOptions.DefaultStartTime, options.StartTime);
        Assert.Equal(TimestampTypes.Milliseconds, options.TimestampType);
    }

    /// <summary>
    /// 默认起始时间固定为 2020-01-01 UTC
    /// </summary>
    [Fact]
    public void DefaultStartTime_IsUtcEpoch2020()
    {
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), NanoIdOptions.DefaultStartTime);
        Assert.Equal(DateTimeKind.Utc, NanoIdOptions.DefaultStartTime.Kind);
    }

    /// <summary>
    /// 长度必须落在 1-128
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(129)]
    public void Size_OutOfRange_Throws(int size)
    {
        var options = new NanoIdOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => { options.Size = size; });
    }

    /// <summary>
    /// 长度的边界取值被接受
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public void Size_AtBoundary_IsAccepted(int size)
    {
        var options = new NanoIdOptions
        {
            Size = size
        };

        Assert.Equal(size, options.Size);
    }

    /// <summary>
    /// 字符集少于 2 个字符时拒绝赋值
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(null)]
    public void Alphabet_WhenTooShort_Throws(string? alphabet)
    {
        var options = new NanoIdOptions();

        Assert.Throws<ArgumentException>(() => { options.Alphabet = alphabet!; });
    }

    /// <summary>
    /// 字符集含重复字符时拒绝赋值，避免概率分布被悄悄扭曲
    /// </summary>
    [Fact]
    public void Alphabet_WithDuplicatedCharacters_Throws()
    {
        var options = new NanoIdOptions();

        Assert.Throws<ArgumentException>(() => { options.Alphabet = "aab"; });
    }

    /// <summary>
    /// 恰好 2 个不重复字符的字符集是允许的最小集合
    /// </summary>
    [Fact]
    public void Alphabet_WithTwoDistinctCharacters_IsAccepted()
    {
        var options = new NanoIdOptions
        {
            Alphabet = "ab"
        };

        Assert.Equal("ab", options.Alphabet);
    }

    /// <summary>
    /// 各预设给出文档承诺的默认长度与字符集
    /// </summary>
    [Fact]
    public void Presets_UseDocumentedSizeAndAlphabet()
    {
        Assert.Equal(10, NanoIdOptions.OnlyNumbers().Size);
        Assert.Equal(NanoIdOptions.NumbersAlphabet, NanoIdOptions.OnlyNumbers().Alphabet);

        Assert.Equal(16, NanoIdOptions.OnlyLowercase().Size);
        Assert.Equal(NanoIdOptions.LowercaseAlphabet, NanoIdOptions.OnlyLowercase().Alphabet);

        Assert.Equal(16, NanoIdOptions.OnlyUppercase().Size);
        Assert.Equal(NanoIdOptions.UppercaseAlphabet, NanoIdOptions.OnlyUppercase().Alphabet);

        Assert.Equal(21, NanoIdOptions.UrlSafe().Size);
        Assert.Equal(NanoIdOptions.UrlSafeAlphabet, NanoIdOptions.UrlSafe().Alphabet);

        Assert.Equal(21, NanoIdOptions.Safe().Size);
        Assert.Equal(NanoIdOptions.SafeAlphabet, NanoIdOptions.Safe().Alphabet);

        Assert.Equal(32, NanoIdOptions.Hex().Size);
        Assert.Equal(NanoIdOptions.HexAlphabet, NanoIdOptions.Hex().Alphabet);
    }

    /// <summary>
    /// 预设允许调用方覆盖长度
    /// </summary>
    [Fact]
    public void Presets_AcceptCustomSize()
    {
        Assert.Equal(6, NanoIdOptions.OnlyNumbers(6).Size);
        Assert.Equal(8, NanoIdOptions.Hex(8).Size);
    }
}
