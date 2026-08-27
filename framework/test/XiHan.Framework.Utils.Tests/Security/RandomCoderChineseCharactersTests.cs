// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 随机汉字生成回归测试
/// </summary>
/// <remarks>
/// 修复前 <see cref="RandomCoder.GetChineseCharacters"/> 走 <c>Encoding.GetEncoding("GB2312")</c>
/// 做区位码换算，而 .NET Core 起 GB2312 不在内置编码提供程序里
/// （需要 System.Text.Encoding.CodePages 包并注册 CodePagesEncodingProvider，本仓两者都没有），
/// 于是该公开方法在任何调用点都直接抛 ArgumentException，等于完全不可用。
/// 现在直接在 CJK 统一表意文字基本区 U+4E00~U+9FA5 取码位，不再依赖任何编码提供程序。
/// </remarks>
public class RandomCoderChineseCharactersTests
{
    /// <summary>
    /// 不抛异常，且按请求长度返回常用汉字
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(64)]
    public void GetChineseCharacters_ReturnsRequestedLengthOfCommonHanzi(int length)
    {
        var text = RandomCoder.GetChineseCharacters(length);

        Assert.Equal(length, text.Length);
        Assert.All(text, c => Assert.InRange(c, '\u4E00', '\u9FA5'));
    }

    /// <summary>
    /// 不传长度时默认取 6 个字
    /// </summary>
    [Fact]
    public void GetChineseCharacters_WithoutLength_ReturnsSixCharacters()
    {
        Assert.Equal(6, RandomCoder.GetChineseCharacters().Length);
        Assert.Equal(6, RandomCoder.GetChineseCharacters(null).Length);
    }

    /// <summary>
    /// 长度为 0 或负数时返回空串（保持修复前后的边界行为一致）
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void GetChineseCharacters_WithNonPositiveLength_ReturnsEmpty(int length)
    {
        Assert.Equal(string.Empty, RandomCoder.GetChineseCharacters(length));
    }

    /// <summary>
    /// 结果不是固定值，随机源确实在起作用
    /// </summary>
    [Fact]
    public void GetChineseCharacters_ProducesVaryingResults()
    {
        var values = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            values.Add(RandomCoder.GetChineseCharacters(8));
        }

        Assert.True(values.Count > 45, $"50 次取值只得到 {values.Count} 个不同结果，随机源可能异常。");
    }

    /// <summary>
    /// 产出的每个字都是单个 BMP 字符，不会出现代理对导致的长度膨胀
    /// </summary>
    [Fact]
    public void GetChineseCharacters_ContainsNoSurrogatePairs()
    {
        var text = RandomCoder.GetChineseCharacters(128);

        Assert.Equal(128, text.Length);
        Assert.All(text, c => Assert.False(char.IsSurrogate(c)));
    }
}
