// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Implementations;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 内存灰度规则仓储测试
/// </summary>
/// <remarks>
/// 该仓储被注册为单例并被引擎并发读取，因此除了增删改查语义，还要覆盖两条隐式契约：
/// GetEnabledRulesAsync 返回的是快照而非活视图；写入在并发下不丢数据。
/// </remarks>
public class InMemoryGrayRuleRepositoryTests
{
    /// <summary>
    /// 新建仓储没有任何规则
    /// </summary>
    [Fact]
    public async Task GetEnabledRulesAsync_OnEmptyRepository_ReturnsEmptyList()
    {
        var repository = new InMemoryGrayRuleRepository();

        var rules = await repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(rules);
        Assert.Empty(rules);
    }

    /// <summary>
    /// 添加后可按规则ID取回
    /// </summary>
    [Fact]
    public async Task AddRule_ThenGetRuleByIdAsync_ReturnsSameInstance()
    {
        var repository = new InMemoryGrayRuleRepository();
        var rule = CreateRule("rule-1", true);
        repository.AddRule(rule);

        var found = await repository.GetRuleByIdAsync("rule-1", TestContext.Current.CancellationToken);

        Assert.Same(rule, found);
    }

    /// <summary>
    /// 查询不存在的规则ID返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public async Task GetRuleByIdAsync_WithUnknownId_ReturnsNull()
    {
        var repository = new InMemoryGrayRuleRepository();
        repository.AddRule(CreateRule("rule-1", true));

        Assert.Null(await repository.GetRuleByIdAsync("missing", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 按ID查询不做启用状态过滤，停用规则同样能取回
    /// </summary>
    /// <remarks>
    /// 管理端需要读回停用规则做编辑，这与 GetEnabledRulesAsync 的过滤语义是两回事。
    /// </remarks>
    [Fact]
    public async Task GetRuleByIdAsync_ReturnsDisabledRuleAsWell()
    {
        var repository = new InMemoryGrayRuleRepository();
        repository.AddRule(CreateRule("rule-1", false));

        var found = await repository.GetRuleByIdAsync("rule-1", TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.False(found.IsEnabled);
    }

    /// <summary>
    /// 只返回启用状态的规则
    /// </summary>
    [Fact]
    public async Task GetEnabledRulesAsync_FiltersOutDisabledRules()
    {
        var repository = new InMemoryGrayRuleRepository();
        repository.AddRule(CreateRule("rule-enabled", true));
        repository.AddRule(CreateRule("rule-disabled", false));

        var rules = await repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.Single(rules);
        Assert.Equal("rule-enabled", rules[0].RuleId);
    }

    /// <summary>
    /// 使用相同规则ID再次添加即为覆盖更新
    /// </summary>
    [Fact]
    public async Task AddRule_WithExistingRuleId_ReplacesPreviousRule()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;
        repository.AddRule(CreateRule("rule-1", true, "旧规则"));
        repository.AddRule(CreateRule("rule-1", true, "新规则"));

        var found = await repository.GetRuleByIdAsync("rule-1", token);
        var rules = await repository.GetEnabledRulesAsync(token);

        Assert.NotNull(found);
        Assert.Equal("新规则", found.RuleName);
        Assert.Single(rules);
    }

    /// <summary>
    /// 覆盖更新可以把规则从启用改成停用
    /// </summary>
    [Fact]
    public async Task AddRule_CanFlipRuleFromEnabledToDisabled()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;
        repository.AddRule(CreateRule("rule-1", true));
        repository.AddRule(CreateRule("rule-1", false));

        Assert.Empty(await repository.GetEnabledRulesAsync(token));
    }

    /// <summary>
    /// 移除后既查不到也不再参与启用列表
    /// </summary>
    [Fact]
    public async Task RemoveRule_DropsRuleFromBothQueries()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;
        repository.AddRule(CreateRule("rule-1", true));
        repository.AddRule(CreateRule("rule-2", true));

        repository.RemoveRule("rule-1");

        Assert.Null(await repository.GetRuleByIdAsync("rule-1", token));
        var rules = await repository.GetEnabledRulesAsync(token);
        Assert.Single(rules);
        Assert.Equal("rule-2", rules[0].RuleId);
    }

    /// <summary>
    /// 移除不存在的规则是静默空操作
    /// </summary>
    [Fact]
    public async Task RemoveRule_WithUnknownId_IsSilentNoOp()
    {
        var repository = new InMemoryGrayRuleRepository();
        repository.AddRule(CreateRule("rule-1", true));

        repository.RemoveRule("missing");
        repository.RemoveRule("missing");

        Assert.Single((await repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// 清空会连停用规则一起删除
    /// </summary>
    [Fact]
    public async Task Clear_RemovesEveryRuleIncludingDisabledOnes()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;
        repository.AddRule(CreateRule("rule-1", true));
        repository.AddRule(CreateRule("rule-2", false));

        repository.Clear();

        Assert.Empty(await repository.GetEnabledRulesAsync(token));
        Assert.Null(await repository.GetRuleByIdAsync("rule-2", token));
    }

    /// <summary>
    /// 刷新是空操作，不会丢掉已有规则
    /// </summary>
    /// <remarks>
    /// 内存实现没有外部数据源，RefreshAsync 必须是幂等空操作；若哪天改成「重载」会直接清空线上规则。
    /// </remarks>
    [Fact]
    public async Task RefreshAsync_IsNoOpAndKeepsRules()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;
        repository.AddRule(CreateRule("rule-1", true));

        await repository.RefreshAsync(token);
        await repository.RefreshAsync(token);

        Assert.Single((await repository.GetEnabledRulesAsync(token)));
    }

    /// <summary>
    /// 返回的是快照列表，改动它不会影响仓储，也不会被后续写入反向影响
    /// </summary>
    [Fact]
    public async Task GetEnabledRulesAsync_ReturnsDetachedSnapshot()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;
        repository.AddRule(CreateRule("rule-1", true));

        var first = await repository.GetEnabledRulesAsync(token);
        first.Clear();
        repository.AddRule(CreateRule("rule-2", true));
        var second = await repository.GetEnabledRulesAsync(token);

        Assert.Empty(first);
        Assert.Equal(2, second.Count);
    }

    /// <summary>
    /// 并发写入不同规则ID时全部落库
    /// </summary>
    [Fact]
    public async Task AddRule_UnderConcurrency_KeepsEveryDistinctRule()
    {
        const int count = 500;

        var repository = new InMemoryGrayRuleRepository();

        Parallel.For(0, count, index => repository.AddRule(CreateRule("rule-" + index, true)));

        var rules = await repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(count, rules.Count);
        Assert.Equal(count, rules.Select(rule => rule.RuleId).Distinct().Count());
    }

    /// <summary>
    /// 并发地对同一批规则做「加了又删」不会抛异常，最终收敛为空
    /// </summary>
    [Fact]
    public async Task AddAndRemove_UnderConcurrency_ConvergeWithoutThrowing()
    {
        const int count = 200;

        var repository = new InMemoryGrayRuleRepository();
        for (var index = 0; index < count; index++)
        {
            repository.AddRule(CreateRule("rule-" + index, true));
        }

        Parallel.For(0, count, index =>
        {
            repository.AddRule(CreateRule("rule-" + index, true));
            repository.RemoveRule("rule-" + index);
        });

        Assert.Empty(await repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 边写边读不会抛出「集合已被修改」类异常
    /// </summary>
    /// <remarks>
    /// 引擎在单例仓储上并发读取，而管理端可能同时写入，这条用来兜住底层容器换成非并发字典的风险。
    /// </remarks>
    [Fact]
    public async Task GetEnabledRulesAsync_WhileWriting_DoesNotThrow()
    {
        var repository = new InMemoryGrayRuleRepository();
        var token = TestContext.Current.CancellationToken;

        var writer = Task.Run(() =>
        {
            for (var index = 0; index < 2000; index++)
            {
                repository.AddRule(CreateRule("rule-" + index, true));
            }
        }, token);

        for (var round = 0; round < 200; round++)
        {
            var rules = await repository.GetEnabledRulesAsync(token);
            Assert.All(rules, rule => Assert.True(rule.IsEnabled));
        }

        await writer;

        Assert.Equal(2000, (await repository.GetEnabledRulesAsync(token)).Count);
    }

    /// <summary>
    /// 构造一条规则
    /// </summary>
    private static GrayRule CreateRule(string ruleId, bool isEnabled, string ruleName = "灰度规则")
    {
        return new GrayRule
        {
            RuleId = ruleId,
            RuleName = ruleName,
            RuleType = GrayRuleType.Header,
            IsEnabled = isEnabled,
            Priority = 1
        };
    }
}
