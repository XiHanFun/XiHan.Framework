// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.Tests.Fakes;

namespace XiHan.Framework.Traffic.Tests.GrayRouting.Matchers;

/// <summary>
/// 百分比灰度匹配器测试
/// </summary>
/// <remarks>
/// 当前实现基于 Random.Shared 抽样（非按 key 哈希分桶），因此只能断言两类东西：
/// 一是确定性边界（0/负数/缺失配置必不命中，100 及以上必命中），
/// 二是大样本下命中率落在统计区间内——绝不断言单次结果或「同一上下文多次结果一致」。
/// </remarks>
public class PercentageGrayMatcherTests
{
    /// <summary>
    /// 匹配器声明的规则类型是百分比
    /// </summary>
    [Fact]
    public void RuleType_IsPercentage()
    {
        Assert.Equal(GrayRuleType.Percentage, new PercentageGrayMatcher().RuleType);
    }

    /// <summary>
    /// 百分比为 100 时必然命中
    /// </summary>
    /// <remarks>
    /// 随机取值域是 [1,100]，100% 是唯一可确定性断言「必命中」的配置。
    /// </remarks>
    [Fact]
    public void IsMatch_WhenPercentageIsHundred_AlwaysMatches()
    {
        var matcher = new PercentageGrayMatcher();
        var rule = CreateRule("""{"Percentage":100}""");

        for (var index = 0; index < 200; index++)
        {
            Assert.True(matcher.IsMatch(new GrayContext(), rule));
        }
    }

    /// <summary>
    /// 百分比超过 100 时同样必然命中，不会因越界而抛异常
    /// </summary>
    [Fact]
    public void IsMatch_WhenPercentageExceedsHundred_AlwaysMatches()
    {
        var matcher = new PercentageGrayMatcher();
        var rule = CreateRule("""{"Percentage":150}""");

        for (var index = 0; index < 200; index++)
        {
            Assert.True(matcher.IsMatch(new GrayContext(), rule));
        }
    }

    /// <summary>
    /// 百分比为 0 或负数时必然不命中
    /// </summary>
    [Theory]
    [InlineData("""{"Percentage":0}""")]
    [InlineData("""{"Percentage":-1}""")]
    [InlineData("""{"Percentage":-100}""")]
    public void IsMatch_WhenPercentageIsNotPositive_NeverMatches(string configuration)
    {
        var matcher = new PercentageGrayMatcher();
        var rule = CreateRule(configuration);

        for (var index = 0; index < 200; index++)
        {
            Assert.False(matcher.IsMatch(new GrayContext(), rule));
        }
    }

    /// <summary>
    /// 配置里缺少 Percentage 字段或显式为 null 时不命中
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Percentage":null}""")]
    [InlineData("null")]
    public void IsMatch_WhenPercentageIsAbsent_ReturnsFalse(string configuration)
    {
        Assert.False(new PercentageGrayMatcher().IsMatch(new GrayContext(), CreateRule(configuration)));
    }

    /// <summary>
    /// 配置不是合法 JSON 时吞掉异常并返回不命中
    /// </summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("{")]
    [InlineData("""{"Percentage":"abc"}""")]
    public void IsMatch_WhenConfigurationIsMalformed_ReturnsFalse(string configuration)
    {
        Assert.False(new PercentageGrayMatcher().IsMatch(new GrayContext(), CreateRule(configuration)));
    }

    /// <summary>
    /// 配置为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsNull_ReturnsFalse()
    {
        Assert.False(new PercentageGrayMatcher().IsMatch(new GrayContext(), CreateRule(null)));
    }

    /// <summary>
    /// 配置为空串时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsEmpty_ReturnsFalse()
    {
        Assert.False(new PercentageGrayMatcher().IsMatch(new GrayContext(), CreateRule(string.Empty)));
    }

    /// <summary>
    /// 规则不是 GrayRule 实现时不命中
    /// </summary>
    /// <remarks>
    /// 配置串挂在 GrayRule 上，接口 IGrayRule 里没有 Configuration，因此只实现接口的规则必然无法参与百分比判定。
    /// </remarks>
    [Fact]
    public void IsMatch_WhenRuleIsNotGrayRule_ReturnsFalse()
    {
        var rule = new FakeGrayRule { RuleType = GrayRuleType.Percentage };

        Assert.False(new PercentageGrayMatcher().IsMatch(new GrayContext(), rule));
    }

    /// <summary>
    /// 大样本命中率落在配置百分比对应的统计区间内
    /// </summary>
    /// <remarks>
    /// 用宽区间而非精确值：20000 次抽样下三档配置的标准差都在 0.004 以内，
    /// 给出的区间约为 10 倍标准差，既能抓住「分流比例整体跑偏」，又不会随机翻车。
    /// </remarks>
    [Theory]
    [InlineData(10, 0.07, 0.13)]
    [InlineData(50, 0.45, 0.55)]
    [InlineData(90, 0.87, 0.93)]
    public void IsMatch_HitRatio_FallsInsideStatisticalBand(int percentage, double lowerBound, double upperBound)
    {
        const int samples = 20000;

        var matcher = new PercentageGrayMatcher();
        var rule = CreateRule("{\"Percentage\":" + percentage + "}");
        var context = new GrayContext { UserId = 1001L };

        var hits = 0;
        for (var index = 0; index < samples; index++)
        {
            if (matcher.IsMatch(context, rule))
            {
                hits++;
            }
        }

        var ratio = (double)hits / samples;

        Assert.InRange(ratio, lowerBound, upperBound);
    }

    /// <summary>
    /// 异步重载在确定性配置上与同步重载结论一致
    /// </summary>
    [Fact]
    public async Task IsMatchAsync_OnDeterministicConfigurations_MirrorsSyncOverload()
    {
        var matcher = new PercentageGrayMatcher();
        var context = new GrayContext();

        Assert.True(await matcher.IsMatchAsync(context, CreateRule("""{"Percentage":100}"""), TestContext.Current.CancellationToken));
        Assert.False(await matcher.IsMatchAsync(context, CreateRule("""{"Percentage":0}"""), TestContext.Current.CancellationToken));
        Assert.False(await matcher.IsMatchAsync(context, CreateRule(null), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 上下文为空对象时不影响确定性配置的判定结果
    /// </summary>
    /// <remarks>
    /// 百分比匹配不读取上下文任何字段，缺失用户/租户/IP 时也必须照常工作。
    /// </remarks>
    [Fact]
    public void IsMatch_WithEmptyContext_StillHonorsDeterministicConfiguration()
    {
        var matcher = new PercentageGrayMatcher();

        Assert.True(matcher.IsMatch(new GrayContext(), CreateRule("""{"Percentage":100}""")));
        Assert.False(matcher.IsMatch(new GrayContext(), CreateRule("""{"Percentage":0}""")));
    }

    /// <summary>
    /// 构造一条百分比灰度规则
    /// </summary>
    private static GrayRule CreateRule(string? configuration)
    {
        return new GrayRule
        {
            RuleId = "pct-1",
            RuleName = "百分比灰度",
            RuleType = GrayRuleType.Percentage,
            IsEnabled = true,
            Priority = 1,
            Configuration = configuration
        };
    }
}
