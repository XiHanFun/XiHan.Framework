// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 选项解析与区域协商。
/// </summary>
public sealed class LocalizationOptionsTests
{
    /// <summary>
    /// 用键值对构造配置。
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Defaults_AreEnglishAndTenLocales()
    {
        var options = new LocalizationOptions();

        Assert.Equal("en-US", options.DefaultLocale);
        Assert.Equal(SupportedLocales.All, options.Locales);
    }

    [Fact]
    public void FromConfiguration_ParsesAndValidates()
    {
        var options = LocalizationOptions.FromConfiguration(Build(
            ("XiHan:Localization:DefaultLocale", "zh-cn"),
            ("XiHan:Localization:Locales", "zh-cn, en-US ,ja-JP,zh-CN")));

        Assert.Equal("zh-CN", options.DefaultLocale);
        Assert.Equal(["zh-CN", "en-US", "ja-JP"], options.Locales);
        Assert.Equal("en-US", LocalizationOptions.FromConfiguration(Build()).DefaultLocale);

        Assert.Throws<ArgumentException>(() => LocalizationOptions.FromConfiguration(Build(("XiHan:Localization:DefaultLocale", "fr-FR"), ("XiHan:Localization:Locales", "en-US"))));
        Assert.Throws<ArgumentException>(() => LocalizationOptions.FromConfiguration(Build(("XiHan:Localization:Locales", " "))));
        Assert.Throws<ArgumentException>(() => LocalizationOptions.FromConfiguration(Build(("XiHan:Localization:DefaultLocale", "bad locale"))));
    }

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("ZH-cn", "zh-CN")]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-HK", "zh-CN")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("fr", "fr-FR")]
    [InlineData("xx-YY", "en-US")]
    [InlineData("not a locale", "en-US")]
    public void Negotiate_PrefersExactThenLanguageThenDefault(string candidate, string expected)
    {
        Assert.Equal(expected, new LocalizationOptions().Negotiate([candidate]));
    }

    [Fact]
    public void Negotiate_UsesCandidateOrder_AndSkipsInvalidEntries()
    {
        var options = new LocalizationOptions();

        Assert.Equal("ja-JP", options.Negotiate([null, "xx", "ja-JP", "zh-CN"]));
        // 第一轮精确匹配优先于第二轮语言匹配：即使 zh 排在前面，后面精确的 ja-JP 先命中
        Assert.Equal("ja-JP", options.Negotiate(["zh", "ja-JP"]));
        Assert.Equal("en-US", options.Negotiate([]));
    }

    [Fact]
    public void Resolve_ReadsLocaleBaggage()
    {
        var options = new LocalizationOptions();
        var withLocale = TestInvocationContext.Create(baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "ko-kr" });

        Assert.Equal("ko-KR", options.Resolve(withLocale));
        Assert.Equal("en-US", options.Resolve(TestInvocationContext.Create()));
    }
}
