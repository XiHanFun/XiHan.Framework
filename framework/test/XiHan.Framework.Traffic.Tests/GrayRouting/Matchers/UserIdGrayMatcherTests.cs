// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.Tests.Fakes;

namespace XiHan.Framework.Traffic.Tests.GrayRouting.Matchers;

/// <summary>
/// 用户ID灰度匹配器测试
/// </summary>
/// <remarks>
/// 定向灰度是「白名单语义」：只有上下文带用户ID、且配置里显式列出该ID才命中，其余一切情况一律不命中。
/// 配置字段名按 Pascal 书写，与实现使用的默认 JsonSerializerOptions（区分大小写）保持一致。
/// </remarks>
public class UserIdGrayMatcherTests
{
    /// <summary>
    /// 匹配器声明的规则类型是用户ID
    /// </summary>
    [Fact]
    public void RuleType_IsUserId()
    {
        Assert.Equal(GrayRuleType.UserId, new UserIdGrayMatcher().RuleType);
    }

    /// <summary>
    /// 用户ID在白名单内时命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenUserIdIsWhitelisted_ReturnsTrue()
    {
        var context = new GrayContext { UserId = 1002L };

        Assert.True(new UserIdGrayMatcher().IsMatch(context, CreateRule("""{"UserIds":[1001,1002,1003]}""")));
    }

    /// <summary>
    /// 用户ID不在白名单内时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenUserIdIsNotWhitelisted_ReturnsFalse()
    {
        var context = new GrayContext { UserId = 2001L };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, CreateRule("""{"UserIds":[1001,1002,1003]}""")));
    }

    /// <summary>
    /// 上下文没有用户ID时不命中
    /// </summary>
    /// <remarks>
    /// 匿名请求不应该被定向灰度捞进去。
    /// </remarks>
    [Fact]
    public void IsMatch_WhenContextHasNoUserId_ReturnsFalse()
    {
        Assert.False(new UserIdGrayMatcher().IsMatch(new GrayContext(), CreateRule("""{"UserIds":[1001]}""")));
    }

    /// <summary>
    /// 白名单为空数组、显式 null 或字段缺失时不命中
    /// </summary>
    [Theory]
    [InlineData("""{"UserIds":[]}""")]
    [InlineData("""{"UserIds":null}""")]
    [InlineData("{}")]
    [InlineData("null")]
    public void IsMatch_WhenWhitelistIsAbsentOrEmpty_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { UserId = 1001L };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置不是合法 JSON 或类型不匹配时吞异常并返回不命中
    /// </summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("[1001]")]
    [InlineData("""{"UserIds":"1001"}""")]
    public void IsMatch_WhenConfigurationIsMalformed_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { UserId = 1001L };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsNull_ReturnsFalse()
    {
        var context = new GrayContext { UserId = 1001L };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, CreateRule(null)));
    }

    /// <summary>
    /// 配置为空串时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsEmpty_ReturnsFalse()
    {
        var context = new GrayContext { UserId = 1001L };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, CreateRule(string.Empty)));
    }

    /// <summary>
    /// 规则不是 GrayRule 实现时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenRuleIsNotGrayRule_ReturnsFalse()
    {
        var context = new GrayContext { UserId = 1001L };
        var rule = new FakeGrayRule { RuleType = GrayRuleType.UserId };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, rule));
    }

    /// <summary>
    /// 白名单支持 long 全量值域，雪花ID 级别的大整数不会被截断
    /// </summary>
    [Fact]
    public void IsMatch_SupportsFullLongRange()
    {
        var matcher = new UserIdGrayMatcher();
        var rule = CreateRule("""{"UserIds":[9223372036854775807,-9223372036854775808]}""");

        Assert.True(matcher.IsMatch(new GrayContext { UserId = long.MaxValue }, rule));
        Assert.True(matcher.IsMatch(new GrayContext { UserId = long.MinValue }, rule));
        Assert.False(matcher.IsMatch(new GrayContext { UserId = 0L }, rule));
    }

    /// <summary>
    /// 只读取用户ID，不会误用租户ID
    /// </summary>
    [Fact]
    public void IsMatch_IgnoresTenantId()
    {
        var context = new GrayContext { TenantId = 1001L };

        Assert.False(new UserIdGrayMatcher().IsMatch(context, CreateRule("""{"UserIds":[1001]}""")));
    }

    /// <summary>
    /// 异步重载与同步重载结论一致
    /// </summary>
    [Fact]
    public async Task IsMatchAsync_MirrorsSyncOverload()
    {
        var matcher = new UserIdGrayMatcher();
        var rule = CreateRule("""{"UserIds":[1001]}""");
        var token = TestContext.Current.CancellationToken;

        Assert.True(await matcher.IsMatchAsync(new GrayContext { UserId = 1001L }, rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext { UserId = 1002L }, rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext(), rule, token));
    }

    /// <summary>
    /// 构造一条用户ID灰度规则
    /// </summary>
    private static GrayRule CreateRule(string? configuration)
    {
        return new GrayRule
        {
            RuleId = "user-1",
            RuleName = "用户定向灰度",
            RuleType = GrayRuleType.UserId,
            IsEnabled = true,
            Priority = 1,
            Configuration = configuration
        };
    }
}
