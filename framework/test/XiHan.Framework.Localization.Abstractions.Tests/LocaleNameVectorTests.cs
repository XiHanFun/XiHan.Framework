// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 回放 <c>locale-names.json</c>：区域名的文法、规范形式与内置区域
/// </summary>
/// <remarks>
/// <c>localization/locale-names.json</c>（RFC-0010「区域」，ADR-0277）。文法、规范形式与语言子标签经 <see cref="LocaleName"/> 回放；内置的 10 种区域与默认区域同 <see cref="SupportedLocales"/> 比较，也同 <see cref="LocalizationOptions"/> 的缺省值比较（内置区域就是缺省的启用列表）
/// </remarks>
public sealed class LocaleNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly LocaleNameVectors _vectors = SpecVectors.Read<LocaleNameVectors>("localization", "locale-names.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(LocaleName.MaxLength, _vectors.MaxLength);

    [Fact]
    public void BuiltInLocalesAndTheDefault_AreTheOnesInTheVectors()
    {
        Assert.Equal(SupportedLocales.Default, _vectors.Default);
        Assert.Equal(_vectors.BuiltIn, SupportedLocales.All);

        // 内置区域也是缺省的启用列表，按同一个次序；默认区域是其中之一
        var options = new LocalizationOptions();
        Assert.Equal(_vectors.BuiltIn, options.Locales);
        Assert.Equal(_vectors.Default, options.DefaultLocale);
        Assert.Contains(_vectors.Default, _vectors.BuiltIn);

        // 内置区域的写法就是规范形式
        Assert.All(_vectors.BuiltIn, static locale =>
        {
            Assert.True(LocaleName.IsValid(locale), locale);
            Assert.Equal(locale, LocaleName.Normalize(locale));
        });
    }

    [Fact]
    public void IsValid_AcceptsTheValidNames_AndNormalizeGivesTheirCanonicalForm()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var name in _vectors.Valid)
        {
            Assert.True(LocaleName.IsValid(name.Text), name.Text);
            Assert.Equal(name.Canonical, LocaleName.Normalize(name.Text));
            Assert.Equal(name.Canonical, LocaleName.Validate(name.Text));
            Assert.Equal(name.Language, LocaleName.LanguageOf(name.Text));

            // 比较不区分大小写：原文与规范形式是同一个区域，规范形式再规范化不变
            Assert.True(LocaleName.AreEqual(name.Text, name.Canonical), name.Text);
            Assert.Equal(name.Canonical, LocaleName.Normalize(name.Canonical));
        }
    }

    [Fact]
    public void IsValid_AcceptsTheLongestName()
    {
        Assert.Equal(_vectors.MaxLength, _vectors.Longest.Length);
        Assert.True(LocaleName.IsValid(_vectors.Longest), _vectors.Longest);
    }

    [Fact]
    public void IsValid_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var name in _vectors.Invalid)
        {
            Assert.False(LocaleName.IsValid(name), name);
            Assert.Throws<ArgumentException>(() => LocaleName.Validate(name));
        }
    }

    [Fact]
    public void TheVectors_CoverTheLimit_AndEveryKindOfSubtag()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：只因超长而不合法的要拒（去掉末尾一个字符就是那个最长的合法区域名）
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MaxLength + 1 && name[..^1] == _vectors.Longest);

        // 规范形式的三条规则各要有改写了大小写的用例：语言小写、两个字母的地区大写、四个字母的文字首字母大写
        Assert.Contains(_vectors.Valid, static name => name.Text != name.Canonical && !name.Text.Contains('-', StringComparison.Ordinal));
        Assert.Contains(_vectors.Valid, static name => name.Text != name.Canonical && name.Canonical.Split('-') is [_, { Length: 2 }]);
        Assert.Contains(_vectors.Valid, static name => name.Text != name.Canonical && name.Canonical.Split('-') is [_, { Length: 4 }, ..]);

        // 三个字母的语言子标签与数字的地区也要有
        Assert.Contains(_vectors.Valid, static name => name.Language.Length == 3);
        Assert.Contains(_vectors.Valid, static name => name.Canonical.Split('-') is [_, { Length: 3 } region] && region.All(char.IsAsciiDigit));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">区域名的字符数上限</param>
    /// <param name="Default">缺省的默认区域</param>
    /// <param name="BuiltIn">内置的区域，按缺省启用列表的次序</param>
    /// <param name="Valid">合文法的区域名</param>
    /// <param name="Longest">正好到上限的合法区域名</param>
    /// <param name="Invalid">不合文法的区域名</param>
    private sealed record LocaleNameVectors(int MaxLength, string Default, string[] BuiltIn, ValidName[] Valid, string Longest, string[] Invalid);

    /// <summary>
    /// 一个合文法的区域名
    /// </summary>
    /// <param name="Text">原文</param>
    /// <param name="Canonical">规范形式</param>
    /// <param name="Language">语言子标签（小写）</param>
    private sealed record ValidName(string Text, string Canonical, string Language);
}
