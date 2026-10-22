// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 区域名文法与规范化。
/// </summary>
public sealed class LocaleNameTests
{
    [Theory]
    [InlineData("en-US", true)]
    [InlineData("zh", true)]
    [InlineData("zh-Hant-TW", true)]
    [InlineData("pt-BR", true)]
    [InlineData("", false)]
    [InlineData("e", false)]
    [InlineData("en_US", false)]
    [InlineData("en-", false)]
    [InlineData("中文", false)]
    public void IsValid_FollowsBcp47Subset(string value, bool valid)
    {
        Assert.Equal(valid, LocaleName.IsValid(value));
        if (!valid)
        {
            Assert.Throws<ArgumentException>(() => LocaleName.Validate(value));
        }
    }

    [Theory]
    [InlineData("zh-cn", "zh-CN")]
    [InlineData("EN-us", "en-US")]
    [InlineData("ZH-hant-tw", "zh-Hant-TW")]
    [InlineData("es-419", "es-419")]
    [InlineData("ja", "ja")]
    public void Normalize_CanonicalizesCasing(string input, string expected)
    {
        Assert.Equal(expected, LocaleName.Normalize(input));
        Assert.Equal(expected, LocaleName.Validate(input));
    }

    [Fact]
    public void LanguageOf_And_AreEqual_IgnoreCase()
    {
        Assert.Equal("zh", LocaleName.LanguageOf("ZH-TW"));
        Assert.Equal("en", LocaleName.LanguageOf("en"));
        Assert.True(LocaleName.AreEqual("en-us", "EN-US"));
        Assert.False(LocaleName.AreEqual("en-US", "en-GB"));
        Assert.False(LocaleName.IsValid(null));
        Assert.Throws<ArgumentNullException>(static () => LocaleName.Validate(null!));
    }

    [Fact]
    public void SupportedLocales_AreTenWithEnglishDefault()
    {
        Assert.Equal(10, SupportedLocales.All.Count);
        Assert.Equal("en-US", SupportedLocales.Default);
        Assert.Equal(SupportedLocales.Default, SupportedLocales.All[0]);
        Assert.All(SupportedLocales.All, static l => Assert.Equal(l, LocaleName.Normalize(l)));
        Assert.Equal(SupportedLocales.All.Count, SupportedLocales.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
