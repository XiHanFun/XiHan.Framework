// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 请求头灰度匹配器测试
/// </summary>
/// <remarks>
/// 该匹配器有两套语义：配了 HeaderValue 走「忽略大小写的精确比对」，没配（含空串）则退化为「存在即命中」。
/// 头名查找的大小写敏感性由上下文字典的比较器决定，这里一并覆盖。
/// </remarks>
public class HeaderGrayMatcherTests
{
    /// <summary>
    /// 匹配器声明的规则类型是请求头
    /// </summary>
    [Fact]
    public void RuleType_IsHeader()
    {
        Assert.Equal(GrayRuleType.Header, new HeaderGrayMatcher().RuleType);
    }

    /// <summary>
    /// 请求头值与配置期望值一致时命中，比对忽略大小写
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void IsMatch_WhenHeaderValueEqualsIgnoringCase_ReturnsTrue(string headerValue)
    {
        var context = CreateContext(("X-Gray", headerValue));

        Assert.True(new HeaderGrayMatcher().IsMatch(context, CreateRule("""{"HeaderName":"X-Gray","HeaderValue":"true"}""")));
    }

    /// <summary>
    /// 请求头值与配置期望值不一致时不命中
    /// </summary>
    [Theory]
    [InlineData("false")]
    [InlineData("truex")]
    [InlineData(" true")]
    [InlineData("")]
    public void IsMatch_WhenHeaderValueDiffers_ReturnsFalse(string headerValue)
    {
        var context = CreateContext(("X-Gray", headerValue));

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule("""{"HeaderName":"X-Gray","HeaderValue":"true"}""")));
    }

    /// <summary>
    /// 配置里没有期望值时，只要请求头存在就命中
    /// </summary>
    [Theory]
    [InlineData("""{"HeaderName":"X-Gray"}""")]
    [InlineData("""{"HeaderName":"X-Gray","HeaderValue":null}""")]
    [InlineData("""{"HeaderName":"X-Gray","HeaderValue":""}""")]
    public void IsMatch_WhenExpectedValueIsAbsent_MatchesOnHeaderPresence(string configuration)
    {
        var context = CreateContext(("X-Gray", "任意值"));

        Assert.True(new HeaderGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置的请求头在上下文中不存在时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenHeaderIsMissingFromContext_ReturnsFalse()
    {
        var context = CreateContext(("X-Other", "true"));

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule("""{"HeaderName":"X-Gray"}""")));
    }

    /// <summary>
    /// 默认上下文字典忽略大小写，配置里的头名大小写写错也能查到
    /// </summary>
    [Fact]
    public void IsMatch_HeaderNameLookup_UsesContextComparer()
    {
        var context = CreateContext(("X-Gray", "true"));

        Assert.True(new HeaderGrayMatcher().IsMatch(context, CreateRule("""{"HeaderName":"x-gray","HeaderValue":"TRUE"}""")));
    }

    /// <summary>
    /// 上下文请求头为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenHeadersAreNull_ReturnsFalse()
    {
        var context = new GrayContext { Headers = null };

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule("""{"HeaderName":"X-Gray"}""")));
    }

    /// <summary>
    /// 上下文请求头为空集合时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenHeadersAreEmpty_ReturnsFalse()
    {
        Assert.False(new HeaderGrayMatcher().IsMatch(new GrayContext(), CreateRule("""{"HeaderName":"X-Gray"}""")));
    }

    /// <summary>
    /// 配置里缺少头名或头名为空串时不命中
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"HeaderName":""}""")]
    [InlineData("""{"HeaderName":null}""")]
    [InlineData("null")]
    public void IsMatch_WhenHeaderNameIsAbsent_ReturnsFalse(string configuration)
    {
        var context = CreateContext(("X-Gray", "true"));

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置不是合法 JSON 时吞异常并返回不命中
    /// </summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("{")]
    [InlineData("""{"HeaderName":123}""")]
    public void IsMatch_WhenConfigurationIsMalformed_ReturnsFalse(string configuration)
    {
        var context = CreateContext(("X-Gray", "true"));

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsNull_ReturnsFalse()
    {
        var context = CreateContext(("X-Gray", "true"));

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule(null)));
    }

    /// <summary>
    /// 配置为空串时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsEmpty_ReturnsFalse()
    {
        var context = CreateContext(("X-Gray", "true"));

        Assert.False(new HeaderGrayMatcher().IsMatch(context, CreateRule(string.Empty)));
    }

    /// <summary>
    /// 规则不是 GrayRule 实现时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenRuleIsNotGrayRule_ReturnsFalse()
    {
        var context = CreateContext(("X-Gray", "true"));
        var rule = new FakeGrayRule { RuleType = GrayRuleType.Header };

        Assert.False(new HeaderGrayMatcher().IsMatch(context, rule));
    }

    /// <summary>
    /// 上下文换成区分大小写的字典后，头名大小写不一致将不再命中
    /// </summary>
    /// <remarks>
    /// 这条不是在测字典，而是在锁死「匹配器自身不做大小写归一」这一事实，
    /// 调用方自建 Headers 时必须显式使用 OrdinalIgnoreCase。
    /// </remarks>
    [Fact]
    public void IsMatch_WithOrdinalHeaderDictionary_IsHeaderNameCaseSensitive()
    {
        var context = new GrayContext
        {
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Gray"] = "true" }
        };
        var matcher = new HeaderGrayMatcher();

        Assert.True(matcher.IsMatch(context, CreateRule("""{"HeaderName":"X-Gray"}""")));
        Assert.False(matcher.IsMatch(context, CreateRule("""{"HeaderName":"x-gray"}""")));
    }

    /// <summary>
    /// 异步重载与同步重载结论一致
    /// </summary>
    [Fact]
    public async Task IsMatchAsync_MirrorsSyncOverload()
    {
        var matcher = new HeaderGrayMatcher();
        var rule = CreateRule("""{"HeaderName":"X-Gray","HeaderValue":"true"}""");
        var token = TestContext.Current.CancellationToken;

        Assert.True(await matcher.IsMatchAsync(CreateContext(("X-Gray", "TRUE")), rule, token));
        Assert.False(await matcher.IsMatchAsync(CreateContext(("X-Gray", "false")), rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext(), rule, token));
    }

    /// <summary>
    /// 构造一个带指定请求头的上下文
    /// </summary>
    private static GrayContext CreateContext(params (string Name, string Value)[] headers)
    {
        var context = new GrayContext();
        foreach (var (name, value) in headers)
        {
            context.Headers![name] = value;
        }

        return context;
    }

    /// <summary>
    /// 构造一条请求头灰度规则
    /// </summary>
    private static GrayRule CreateRule(string? configuration)
    {
        return new GrayRule
        {
            RuleId = "header-1",
            RuleName = "请求头灰度",
            RuleType = GrayRuleType.Header,
            IsEnabled = true,
            Priority = 1,
            Configuration = configuration
        };
    }
}
