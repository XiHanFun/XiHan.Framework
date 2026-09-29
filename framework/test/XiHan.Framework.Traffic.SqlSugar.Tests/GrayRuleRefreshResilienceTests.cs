// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Time.Testing;
using SqlSugar;
using XiHan.Framework.Traffic.SqlSugar.Entities;

namespace XiHan.Framework.Traffic.SqlSugar.Tests;

/// <summary>
/// 灰度规则缓存单飞刷新与库不可用时的降级测试
/// </summary>
public class GrayRuleRefreshResilienceTests
{
    private const string TableName = "sys_gray_rule";

    /// <summary>
    /// 缓存到期时并发读取只查一次库
    /// </summary>
    [Fact]
    public async Task 缓存到期时并发读取只查一次库()
    {
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMinutes(10));
        context.Client.Insertable(new SysGrayRule("rule-1") { RuleName = "规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();

        var selectCount = 0;
        using var firstSelectEntered = new ManualResetEventSlim(false);
        using var gate = new ManualResetEventSlim(false);
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (!IsGrayRuleSelect(sql))
            {
                return;
            }

            Interlocked.Increment(ref selectCount);
            firstSelectEntered.Set();
            gate.Wait(TimeSpan.FromSeconds(10));
        };

        const int readerCount = 8;
        var readers = Enumerable.Range(0, readerCount)
            .Select(_ => Task.Run(() => context.Repository.GetEnabledRulesAsync()))
            .ToArray();

        Assert.True(firstSelectEntered.Wait(TimeSpan.FromSeconds(10)));
        // 等待其余读取方到达
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        gate.Set();

        var results = await Task.WhenAll(readers);

        Assert.Equal(1, Volatile.Read(ref selectCount));
        Assert.All(results, rules => Assert.Single(rules));
    }

    /// <summary>
    /// 库不可用时保留旧规则并在退避期内不再查库
    /// </summary>
    [Fact]
    public async Task 库不可用时保留旧规则并在退避期内不再查库()
    {
        var time = new FakeTimeProvider();
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMilliseconds(500), timeProvider: time);
        context.Client.Insertable(new SysGrayRule("rule-old") { RuleName = "旧规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        await context.Repository.RefreshAsync(TestContext.Current.CancellationToken);

        var selectCount = 0;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (IsGrayRuleSelect(sql))
            {
                Interlocked.Increment(ref selectCount);
                throw new InvalidOperationException("模拟库宕机");
            }
        };

        time.Advance(TimeSpan.FromMilliseconds(600));
        var afterFailure = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref selectCount));

        time.Advance(TimeSpan.FromMilliseconds(400));
        var duringBackoff = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref selectCount));

        time.Advance(TimeSpan.FromMilliseconds(200));
        var afterBackoff = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref selectCount));
        Assert.Equal("rule-old", Assert.Single(afterFailure).RuleId);
        Assert.Equal("rule-old", Assert.Single(duringBackoff).RuleId);
        Assert.Equal("rule-old", Assert.Single(afterBackoff).RuleId);
    }

    /// <summary>
    /// 刷新间隔较长时退避时间上限为 5 秒
    /// </summary>
    [Fact]
    public async Task 刷新间隔较长时退避时间上限为5秒()
    {
        var time = new FakeTimeProvider();
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMinutes(1), timeProvider: time);
        await context.Repository.RefreshAsync(TestContext.Current.CancellationToken);

        var selectCount = 0;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (IsGrayRuleSelect(sql))
            {
                Interlocked.Increment(ref selectCount);
                throw new InvalidOperationException("模拟库宕机");
            }
        };

        time.Advance(TimeSpan.FromMinutes(1));
        _ = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref selectCount));

        time.Advance(TimeSpan.FromSeconds(4));
        _ = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref selectCount));

        time.Advance(TimeSpan.FromSeconds(1.5));
        _ = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, Volatile.Read(ref selectCount));
    }

    /// <summary>
    /// 刷新间隔极大时不溢出且首次仍会加载
    /// </summary>
    [Fact]
    public async Task 刷新间隔极大时不溢出且首次仍会加载()
    {
        var time = new FakeTimeProvider();
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.MaxValue, timeProvider: time);
        context.Client.Insertable(new SysGrayRule("rule-1") { RuleName = "规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();

        var loaded = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        Assert.Single(loaded);

        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (IsGrayRuleSelect(sql))
            {
                throw new InvalidOperationException("模拟库宕机");
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(() => context.Repository.RefreshAsync(TestContext.Current.CancellationToken));
        var rules = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.Single(rules);
    }

    /// <summary>
    /// 退避结束后恢复查库并换上新规则
    /// </summary>
    [Fact]
    public async Task 退避结束后恢复查库并换上新规则()
    {
        var time = new FakeTimeProvider();
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMilliseconds(300), timeProvider: time);
        context.Client.Insertable(new SysGrayRule("rule-old") { RuleName = "旧规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        await context.Repository.RefreshAsync(TestContext.Current.CancellationToken);

        var failing = true;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (failing && IsGrayRuleSelect(sql))
            {
                throw new InvalidOperationException("模拟库宕机");
            }
        };

        time.Advance(TimeSpan.FromMilliseconds(400));
        _ = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        failing = false;
        context.Client.Insertable(new SysGrayRule("rule-new") { RuleName = "新规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        time.Advance(TimeSpan.FromMilliseconds(300));
        var recovered = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.Contains(recovered, rule => rule.RuleId == "rule-new");
    }

    /// <summary>
    /// 从未成功加载过且库不可用时返回空集合而不抛异常
    /// </summary>
    [Fact]
    public async Task 从未成功加载且库不可用时返回空集合()
    {
        using var context = new GrayRuleTestContext();
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (IsGrayRuleSelect(sql))
            {
                throw new InvalidOperationException("模拟库宕机");
            }
        };

        var rules = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);
        var rule = await context.Repository.GetRuleByIdAsync("any", TestContext.Current.CancellationToken);

        Assert.Empty(rules);
        Assert.Null(rule);
    }

    /// <summary>
    /// 显式刷新在库不可用时向外抛出异常且保留旧规则
    /// </summary>
    [Fact]
    public async Task 显式刷新失败时抛出异常且保留旧规则()
    {
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMinutes(10));
        context.Client.Insertable(new SysGrayRule("rule-old") { RuleName = "旧规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        await context.Repository.RefreshAsync(TestContext.Current.CancellationToken);

        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (IsGrayRuleSelect(sql))
            {
                throw new InvalidOperationException("模拟库宕机");
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(() => context.Repository.RefreshAsync(TestContext.Current.CancellationToken));
        var rules = await context.Repository.GetEnabledRulesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("rule-old", Assert.Single(rules).RuleId);
    }

    private static bool IsGrayRuleSelect(string sql)
    {
        return sql.Contains(TableName, StringComparison.OrdinalIgnoreCase)
            && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
    }
}
