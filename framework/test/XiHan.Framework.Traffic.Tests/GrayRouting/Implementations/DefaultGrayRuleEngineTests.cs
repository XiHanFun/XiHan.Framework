// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Implementations;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 默认灰度规则引擎测试
/// </summary>
/// <remarks>
/// 引擎自身的契约有四条：按 Priority 升序评估、首命中即短路、有效期外的规则连匹配器都不调用、
/// 任何异常都降级为「不灰度」而不是把请求打挂。这里用可观测的匹配器替身把这四条逐一钉死。
/// </remarks>
public class DefaultGrayRuleEngineTests
{
    /// <summary>
    /// 仓储没有启用规则时给出专用原因
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenNoEnabledRules_ReturnsNotGrayWithDedicatedReason()
    {
        var engine = CreateEngine(new FakeGrayRuleRepository());

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.Equal("没有启用的灰度规则", decision.Reason);
        Assert.Null(decision.TargetVersion);
        Assert.Null(decision.MatchedRuleId);
    }

    /// <summary>
    /// 找不到规则类型对应的匹配器时跳过该规则，最终按未命中收尾
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenNoMatcherRegisteredForRuleType_SkipsRule()
    {
        var engine = CreateEngine(new FakeGrayRuleRepository(CreateRule("rule-1", GrayRuleType.Header, 1)));

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.Equal("未命中任何灰度规则", decision.Reason);
    }

    /// <summary>
    /// 按优先级数值升序评估，命中第一条后立即短路
    /// </summary>
    [Fact]
    public async Task DecideAsync_EvaluatesByAscendingPriorityAndStopsAtFirstHit()
    {
        var lowPriority = CreateRule("rule-low", GrayRuleType.Header, 100, "v-low");
        var highPriority = CreateRule("rule-high", GrayRuleType.Header, 1, "v-high");
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(new FakeGrayRuleRepository(lowPriority, highPriority), matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsGray);
        Assert.Equal("rule-high", decision.MatchedRuleId);
        Assert.Equal("v-high", decision.TargetVersion);
        Assert.Equal(1, matcher.InvokedRuleIds.Count);
        Assert.Equal("rule-high", matcher.InvokedRuleIds[0]);
    }

    /// <summary>
    /// 负优先级排在正优先级之前
    /// </summary>
    [Fact]
    public async Task DecideAsync_TreatsNegativePriorityAsHigherPrecedence()
    {
        var normal = CreateRule("rule-normal", GrayRuleType.Header, 0, "v-normal");
        var urgent = CreateRule("rule-urgent", GrayRuleType.Header, -10, "v-urgent");
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(new FakeGrayRuleRepository(normal, urgent), matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.Equal("rule-urgent", decision.MatchedRuleId);
    }

    /// <summary>
    /// 优先级相同时保持仓储返回顺序
    /// </summary>
    /// <remarks>
    /// 依赖 OrderBy 的稳定排序；若改成 Sort/OrderByDescending 之类的不稳定写法，同优先级规则的命中会随机漂移。
    /// </remarks>
    [Fact]
    public async Task DecideAsync_WithEqualPriority_KeepsRepositoryOrder()
    {
        var first = CreateRule("rule-a", GrayRuleType.Header, 5, "va");
        var second = CreateRule("rule-b", GrayRuleType.Header, 5, "vb");
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(new FakeGrayRuleRepository(first, second), matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.Equal("rule-a", decision.MatchedRuleId);
    }

    /// <summary>
    /// 尚未到生效时间的规则被跳过，且不会调用匹配器
    /// </summary>
    [Fact]
    public async Task DecideAsync_SkipsRuleBeforeItsEffectiveTime()
    {
        var pending = CreateRule("rule-pending", GrayRuleType.Header, 1, "v-pending");
        pending.EffectiveTime = DateTime.UtcNow.AddHours(1);
        var active = CreateRule("rule-active", GrayRuleType.Header, 2, "v-active");
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(new FakeGrayRuleRepository(pending, active), matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.Equal("rule-active", decision.MatchedRuleId);
        Assert.Equal(1, matcher.InvokedRuleIds.Count);
        Assert.Equal("rule-active", matcher.InvokedRuleIds[0]);
    }

    /// <summary>
    /// 已过失效时间的规则被跳过
    /// </summary>
    [Fact]
    public async Task DecideAsync_SkipsRuleAfterItsExpiryTime()
    {
        var expired = CreateRule("rule-expired", GrayRuleType.Header, 1, "v-expired");
        expired.ExpiryTime = DateTime.UtcNow.AddHours(-1);
        var active = CreateRule("rule-active", GrayRuleType.Header, 2, "v-active");
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(new FakeGrayRuleRepository(expired, active), matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.Equal("rule-active", decision.MatchedRuleId);
    }

    /// <summary>
    /// 生效窗口覆盖当前时刻的规则正常参与匹配
    /// </summary>
    [Fact]
    public async Task DecideAsync_AcceptsRuleWhoseWindowCoversNow()
    {
        var rule = CreateRule("rule-1", GrayRuleType.Header, 1, "v2");
        rule.EffectiveTime = DateTime.UtcNow.AddHours(-1);
        rule.ExpiryTime = DateTime.UtcNow.AddHours(1);
        var engine = CreateEngine(
            new FakeGrayRuleRepository(rule),
            new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true));

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsGray);
        Assert.Equal("v2", decision.TargetVersion);
    }

    /// <summary>
    /// 所有规则都在有效期外时按未命中收尾
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenEveryRuleIsOutOfWindow_ReturnsNotGray()
    {
        var expired = CreateRule("rule-expired", GrayRuleType.Header, 1, "v1");
        expired.ExpiryTime = DateTime.UtcNow.AddDays(-1);
        var pending = CreateRule("rule-pending", GrayRuleType.Header, 2, "v2");
        pending.EffectiveTime = DateTime.UtcNow.AddDays(1);
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(new FakeGrayRuleRepository(expired, pending), matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.Equal("未命中任何灰度规则", decision.Reason);
        Assert.Equal(0, matcher.InvokedRuleIds.Count);
    }

    /// <summary>
    /// 命中规则没有配置目标版本时回退到 gray
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenMatchedRuleHasNoTargetVersion_FallsBackToGrayLiteral()
    {
        var engine = CreateEngine(
            new FakeGrayRuleRepository(CreateRule("rule-1", GrayRuleType.Header, 1)),
            new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true));

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsGray);
        Assert.Equal("gray", decision.TargetVersion);
    }

    /// <summary>
    /// 命中的规则只实现 IGrayRule 时同样回退到 gray，并保留规则ID
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenMatchedRuleIsNotGrayRule_FallsBackToGrayLiteral()
    {
        var rule = new FakeGrayRule { RuleId = "fake-1", RuleName = "自定义规则", RuleType = GrayRuleType.Custom, Priority = 1 };
        var engine = CreateEngine(
            new FakeGrayRuleRepository(rule),
            new FakeGrayMatcher(GrayRuleType.Custom, (_, _) => true));

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsGray);
        Assert.Equal("gray", decision.TargetVersion);
        Assert.Equal("fake-1", decision.MatchedRuleId);
    }

    /// <summary>
    /// 命中原因引用规则名称，便于排障时直接定位配置
    /// </summary>
    [Fact]
    public async Task DecideAsync_HitReason_QuotesRuleName()
    {
        var rule = CreateRule("rule-1", GrayRuleType.Header, 1, "v2", "请求头灰度");
        var engine = CreateEngine(
            new FakeGrayRuleRepository(rule),
            new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true));

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.Equal("命中规则: 请求头灰度", decision.Reason);
    }

    /// <summary>
    /// 按规则类型挑匹配器，而不是按注册顺序取第一个
    /// </summary>
    [Fact]
    public async Task DecideAsync_SelectsMatcherByRuleTypeNotByRegistrationOrder()
    {
        var headerMatcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var userMatcher = new FakeGrayMatcher(GrayRuleType.UserId, (_, _) => true);
        var engine = CreateEngine(
            new FakeGrayRuleRepository(CreateRule("rule-1", GrayRuleType.UserId, 1, "v2")),
            headerMatcher,
            userMatcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsGray);
        Assert.Equal(0, headerMatcher.InvokedRuleIds.Count);
        Assert.Equal(1, userMatcher.InvokedRuleIds.Count);
    }

    /// <summary>
    /// 全部规则都不命中时按顺序评估完所有规则
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenNothingMatches_EvaluatesEveryRuleInPriorityOrder()
    {
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => false);
        var engine = CreateEngine(
            new FakeGrayRuleRepository(
                CreateRule("rule-third", GrayRuleType.Header, 30),
                CreateRule("rule-first", GrayRuleType.Header, 10),
                CreateRule("rule-second", GrayRuleType.Header, 20)),
            matcher);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.Equal("未命中任何灰度规则", decision.Reason);
        Assert.Equal(3, matcher.InvokedRuleIds.Count);
        Assert.Equal("rule-first", matcher.InvokedRuleIds[0]);
        Assert.Equal("rule-second", matcher.InvokedRuleIds[1]);
        Assert.Equal("rule-third", matcher.InvokedRuleIds[2]);
    }

    /// <summary>
    /// 仓储故障降级为不灰度，并把异常信息带进原因
    /// </summary>
    /// <remarks>
    /// 灰度只是路由增强，规则源不可用时必须回落到稳定版本而不是让请求失败。
    /// </remarks>
    [Fact]
    public async Task DecideAsync_WhenRepositoryThrows_DegradesToNotGray()
    {
        var repository = new FakeGrayRuleRepository
        {
            GetEnabledRulesException = new InvalidOperationException("规则仓储不可用")
        };
        var engine = CreateEngine(repository);

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.StartsWith("决策异常:", decision.Reason);
        Assert.Contains("规则仓储不可用", decision.Reason);
    }

    /// <summary>
    /// 匹配器故障同样降级为不灰度
    /// </summary>
    [Fact]
    public async Task DecideAsync_WhenMatcherThrows_DegradesToNotGray()
    {
        var engine = CreateEngine(
            new FakeGrayRuleRepository(CreateRule("rule-1", GrayRuleType.Header, 1, "v2")),
            new FakeGrayMatcher(GrayRuleType.Header, (_, _) => throw new InvalidOperationException("匹配器故障")));

        var decision = await engine.DecideAsync(new GrayContext(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.Null(decision.TargetVersion);
        Assert.StartsWith("决策异常:", decision.Reason);
        Assert.Contains("匹配器故障", decision.Reason);
    }

    /// <summary>
    /// 取消令牌原样透传给仓储与匹配器
    /// </summary>
    [Fact]
    public async Task DecideAsync_PassesCancellationTokenToRepositoryAndMatcher()
    {
        using var cts = new CancellationTokenSource();
        var repository = new FakeGrayRuleRepository(CreateRule("rule-1", GrayRuleType.Header, 1, "v2"));
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (_, _) => true);
        var engine = CreateEngine(repository, matcher);

        await engine.DecideAsync(new GrayContext(), cts.Token);

        Assert.Equal(cts.Token, repository.LastToken);
        Assert.Equal(1, matcher.ReceivedTokens.Count);
        Assert.Equal(cts.Token, matcher.ReceivedTokens[0]);
    }

    /// <summary>
    /// 上下文原样透传给匹配器
    /// </summary>
    [Fact]
    public async Task DecideAsync_PassesContextThroughToMatcher()
    {
        GrayContext? observed = null;
        var context = new GrayContext { UserId = 1001L, ClientIpAddress = "10.0.0.1" };
        var matcher = new FakeGrayMatcher(GrayRuleType.Header, (received, _) =>
        {
            observed = received;
            return false;
        });
        var engine = CreateEngine(new FakeGrayRuleRepository(CreateRule("rule-1", GrayRuleType.Header, 1)), matcher);

        await engine.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Same(context, observed);
    }

    /// <summary>
    /// 与内置匹配器、内存仓储串起来端到端可用
    /// </summary>
    /// <remarks>
    /// 前面的用例都用替身隔离引擎逻辑，这条补一次真实装配，防止「各自都对、连起来不对」。
    /// </remarks>
    [Fact]
    public async Task DecideAsync_WithBuiltInMatchers_RoutesByUserIdRule()
    {
        var token = TestContext.Current.CancellationToken;
        var repository = new InMemoryGrayRuleRepository();
        repository.AddRule(new GrayRule
        {
            RuleId = "user-rule",
            RuleName = "用户定向灰度",
            RuleType = GrayRuleType.UserId,
            IsEnabled = true,
            Priority = 1,
            TargetVersion = "v2",
            Configuration = """{"UserIds":[1001]}"""
        });

        var engine = new DefaultGrayRuleEngine(
            repository,
            new IGrayMatcher[]
            {
                new PercentageGrayMatcher(),
                new UserIdGrayMatcher(),
                new TenantIdGrayMatcher(),
                new HeaderGrayMatcher(),
                new IpAddressGrayMatcher()
            },
            NullLogger<DefaultGrayRuleEngine>.Instance);

        var hit = await engine.DecideAsync(new GrayContext { UserId = 1001L }, token);
        var miss = await engine.DecideAsync(new GrayContext { UserId = 2002L }, token);

        Assert.True(hit.IsGray);
        Assert.Equal("v2", hit.TargetVersion);
        Assert.Equal("user-rule", hit.MatchedRuleId);
        Assert.False(miss.IsGray);
        Assert.Equal("未命中任何灰度规则", miss.Reason);
    }

    /// <summary>
    /// 停用的规则不会进入引擎评估
    /// </summary>
    [Fact]
    public async Task DecideAsync_WithDisabledRuleInRepository_ReturnsNoEnabledRules()
    {
        var repository = new InMemoryGrayRuleRepository();
        repository.AddRule(new GrayRule
        {
            RuleId = "user-rule",
            RuleName = "用户定向灰度",
            RuleType = GrayRuleType.UserId,
            IsEnabled = false,
            Priority = 1,
            TargetVersion = "v2",
            Configuration = """{"UserIds":[1001]}"""
        });

        var engine = new DefaultGrayRuleEngine(
            repository,
            new IGrayMatcher[] { new UserIdGrayMatcher() },
            NullLogger<DefaultGrayRuleEngine>.Instance);

        var decision = await engine.DecideAsync(new GrayContext { UserId = 1001L }, TestContext.Current.CancellationToken);

        Assert.False(decision.IsGray);
        Assert.Equal("没有启用的灰度规则", decision.Reason);
    }

    /// <summary>
    /// 构造引擎
    /// </summary>
    private static DefaultGrayRuleEngine CreateEngine(IGrayRuleRepository repository, params IGrayMatcher[] matchers)
    {
        return new DefaultGrayRuleEngine(repository, matchers, NullLogger<DefaultGrayRuleEngine>.Instance);
    }

    /// <summary>
    /// 构造一条规则
    /// </summary>
    private static GrayRule CreateRule(
        string ruleId,
        GrayRuleType ruleType,
        int priority,
        string? targetVersion = null,
        string ruleName = "灰度规则")
    {
        return new GrayRule
        {
            RuleId = ruleId,
            RuleName = ruleName,
            RuleType = ruleType,
            IsEnabled = true,
            Priority = priority,
            TargetVersion = targetVersion
        };
    }
}
