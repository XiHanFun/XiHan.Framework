// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.Tests.Fakes;

namespace XiHan.Framework.Traffic.Tests.GrayRouting.Matchers;

/// <summary>
/// 租户ID灰度匹配器测试
/// </summary>
/// <remarks>
/// 租户定向灰度是 SaaS 场景下最常用的「按客户放量」手段，语义与用户定向一致：显式白名单，缺一不命中。
/// </remarks>
public class TenantIdGrayMatcherTests
{
    /// <summary>
    /// 匹配器声明的规则类型是租户ID
    /// </summary>
    [Fact]
    public void RuleType_IsTenantId()
    {
        Assert.Equal(GrayRuleType.TenantId, new TenantIdGrayMatcher().RuleType);
    }

    /// <summary>
    /// 租户ID在白名单内时命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenTenantIdIsWhitelisted_ReturnsTrue()
    {
        var context = new GrayContext { TenantId = 2002L };

        Assert.True(new TenantIdGrayMatcher().IsMatch(context, CreateRule("""{"TenantIds":[2001,2002]}""")));
    }

    /// <summary>
    /// 租户ID不在白名单内时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenTenantIdIsNotWhitelisted_ReturnsFalse()
    {
        var context = new GrayContext { TenantId = 3003L };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, CreateRule("""{"TenantIds":[2001,2002]}""")));
    }

    /// <summary>
    /// 上下文没有租户ID（宿主/平台级请求）时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenContextHasNoTenantId_ReturnsFalse()
    {
        Assert.False(new TenantIdGrayMatcher().IsMatch(new GrayContext(), CreateRule("""{"TenantIds":[2001]}""")));
    }

    /// <summary>
    /// 白名单为空数组、显式 null 或字段缺失时不命中
    /// </summary>
    [Theory]
    [InlineData("""{"TenantIds":[]}""")]
    [InlineData("""{"TenantIds":null}""")]
    [InlineData("{}")]
    [InlineData("null")]
    public void IsMatch_WhenWhitelistIsAbsentOrEmpty_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { TenantId = 2001L };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置不是合法 JSON 或类型不匹配时吞异常并返回不命中
    /// </summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("[2001]")]
    [InlineData("""{"TenantIds":"2001"}""")]
    public void IsMatch_WhenConfigurationIsMalformed_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { TenantId = 2001L };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsNull_ReturnsFalse()
    {
        var context = new GrayContext { TenantId = 2001L };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, CreateRule(null)));
    }

    /// <summary>
    /// 配置为空串时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsEmpty_ReturnsFalse()
    {
        var context = new GrayContext { TenantId = 2001L };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, CreateRule(string.Empty)));
    }

    /// <summary>
    /// 规则不是 GrayRule 实现时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenRuleIsNotGrayRule_ReturnsFalse()
    {
        var context = new GrayContext { TenantId = 2001L };
        var rule = new FakeGrayRule { RuleType = GrayRuleType.TenantId };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, rule));
    }

    /// <summary>
    /// 只读取租户ID，不会误用用户ID
    /// </summary>
    /// <remarks>
    /// 用户ID与租户ID都是 long，串用会造成跨租户误放量，属高危方向，必须锁死。
    /// </remarks>
    [Fact]
    public void IsMatch_IgnoresUserId()
    {
        var context = new GrayContext { UserId = 2001L };

        Assert.False(new TenantIdGrayMatcher().IsMatch(context, CreateRule("""{"TenantIds":[2001]}""")));
    }

    /// <summary>
    /// 白名单支持 long 全量值域
    /// </summary>
    [Fact]
    public void IsMatch_SupportsFullLongRange()
    {
        var matcher = new TenantIdGrayMatcher();
        var rule = CreateRule("""{"TenantIds":[9223372036854775807]}""");

        Assert.True(matcher.IsMatch(new GrayContext { TenantId = long.MaxValue }, rule));
        Assert.False(matcher.IsMatch(new GrayContext { TenantId = long.MaxValue - 1 }, rule));
    }

    /// <summary>
    /// 异步重载与同步重载结论一致
    /// </summary>
    [Fact]
    public async Task IsMatchAsync_MirrorsSyncOverload()
    {
        var matcher = new TenantIdGrayMatcher();
        var rule = CreateRule("""{"TenantIds":[2001]}""");
        var token = TestContext.Current.CancellationToken;

        Assert.True(await matcher.IsMatchAsync(new GrayContext { TenantId = 2001L }, rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext { TenantId = 2002L }, rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext(), rule, token));
    }

    /// <summary>
    /// 构造一条租户ID灰度规则
    /// </summary>
    private static GrayRule CreateRule(string? configuration)
    {
        return new GrayRule
        {
            RuleId = "tenant-1",
            RuleName = "租户定向灰度",
            RuleType = GrayRuleType.TenantId,
            IsEnabled = true,
            Priority = 1,
            Configuration = configuration
        };
    }
}
