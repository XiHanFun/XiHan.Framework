// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using XiHan.Framework.Authorization.Conditions;
using static XiHan.Framework.Authorization.Tests.Infrastructure.ConditionTestKit;

namespace XiHan.Framework.Authorization.Tests.Conditions;

/// <summary>
/// 条件求值器测试
/// </summary>
/// <remarks>
/// 锁死三条容易写错的语义：取不到值是「无法判定」（按不命中计且取反不翻转）；
/// 「待定」让组待定，但组里只要有不命中就不命中；条件集任一组命中即命中，否则有待定即待定。
/// </remarks>
public class DefaultConditionEvaluatorTests
{
    /// <summary>
    /// 字符串等于不区分大小写；不等于是等于的否定
    /// </summary>
    [Theory]
    [InlineData(ConditionOperator.Equals, "ADMIN", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.Equals, "guest", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.NotEquals, "admin", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.NotEquals, "guest", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.Contains, "dmi", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.NotContains, "dmi", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.StartsWith, "AD", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.EndsWith, "IN", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.EndsWith, "ad", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.In, "root,admin", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.NotIn, "root,admin", ConditionOutcome.NotMatched)]
    public async Task String_Operators(ConditionOperator @operator, string value, ConditionOutcome expected)
    {
        var outcome = await EvaluateAsync(new ConditionClause("subject.userName", @operator, value), Values(("subject.userName", "admin")));

        Assert.Equal(expected, outcome);
    }

    /// <summary>
    /// 数字按十进制比较，属性值可以是任意数值类型或数字文本
    /// </summary>
    [Theory]
    [InlineData(ConditionOperator.GreaterThan, "9999.5", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.GreaterThanOrEquals, "10000", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.LessThan, "10000", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.LessThanOrEquals, "10000.00", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.Between, "9,18", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.Between, "{\"min\":10000,\"max\":20000}", ConditionOutcome.Matched)]
    public async Task Number_Operators(ConditionOperator @operator, string value, ConditionOutcome expected)
    {
        Assert.Equal(expected, await EvaluateAsync(new ConditionClause("resource.amount", @operator, value), Values(("resource.amount", 10000))));
        Assert.Equal(expected, await EvaluateAsync(new ConditionClause("resource.amount", @operator, value), Values(("resource.amount", "10000"))));
    }

    /// <summary>
    /// 多值属性：等于、包含、在集合中都是「任一元素」；空列表是有值（不是取不到）
    /// </summary>
    [Fact]
    public async Task MultiValued_AnyElement()
    {
        var roles = Values(("subject.roleCodes", new[] { "employee", "Auditor" }));

        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.Equals, "auditor"), roles));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.Contains, "employee"), roles));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.Contains, "emp"), roles));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.In, "[\"admin\",\"auditor\"]"), roles));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.NotIn, "admin,auditor"), roles));

        var none = Values(("subject.roleCodes", Array.Empty<string>()));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.Contains, "admin"), none));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.NotContains, "admin"), none));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.roleCodes", ConditionOperator.IsNull, null), none));
    }

    /// <summary>
    /// 多值数字：包含是元素相等，比较是任一元素满足
    /// </summary>
    [Fact]
    public async Task MultiValued_Numbers()
    {
        var levels = Values(("subject.levels", new object[] { 1, 3L, 5.0m }));

        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.levels", ConditionOperator.Contains, "3"), levels));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.levels", ConditionOperator.GreaterThan, "4"), levels));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.levels", ConditionOperator.GreaterThan, "5"), levels));
    }

    /// <summary>
    /// IP：集合按 CIDR 包含，IPv4 映射的 IPv6 地址按 IPv4 比较，IPv6 网段同样可用
    /// </summary>
    [Theory]
    [InlineData("203.0.113.9", ConditionOutcome.Matched)]
    [InlineData("::ffff:203.0.113.9", ConditionOutcome.Matched)]
    [InlineData("198.51.100.1", ConditionOutcome.NotMatched)]
    [InlineData("2001:db8::1", ConditionOutcome.Matched)]
    public async Task Ip_InNetworks(string address, ConditionOutcome expected)
    {
        var clause = new ConditionClause("environment.ip", ConditionOperator.In, "203.0.113.0/24, 2001:db8::/32");

        Assert.Equal(expected, await EvaluateAsync(clause, Values(("environment.ip", IPAddress.Parse(address)))));
        Assert.Equal(expected, await EvaluateAsync(clause, Values(("environment.ip", address))));
    }

    /// <summary>
    /// IP 等于按地址比较，映射地址同样还原
    /// </summary>
    [Fact]
    public async Task Ip_Equals()
    {
        var clause = new ConditionClause("environment.ip", ConditionOperator.Equals, "203.0.113.9");

        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(clause, Values(("environment.ip", IPAddress.Parse("::ffff:203.0.113.9")))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(clause, Values(("environment.ip", "203.0.113.10"))));
    }

    /// <summary>
    /// 时刻区间：下限大于上限表示跨零点，两端都包含
    /// </summary>
    [Theory]
    [InlineData("23:30", ConditionOutcome.Matched)]
    [InlineData("06:00", ConditionOutcome.Matched)]
    [InlineData("22:00", ConditionOutcome.Matched)]
    [InlineData("12:00", ConditionOutcome.NotMatched)]
    public async Task TimeOfDay_BetweenAcrossMidnight(string time, ConditionOutcome expected)
    {
        var outcome = await EvaluateAsync(
            new ConditionClause("environment.time", ConditionOperator.Between, "22:00,06:00"),
            Values(("environment.time", TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture))));

        Assert.Equal(expected, outcome);
    }

    /// <summary>
    /// 日期时间属性按编译时区折算成日期、时刻：UTC 16:30 在东八区已是次日 00:30
    /// </summary>
    [Fact]
    public async Task DateAndTime_UseCompileTimeZone()
    {
        var now = new DateTimeOffset(2026, 1, 1, 16, 30, 0, TimeSpan.Zero);

        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(
            new ConditionClause("environment.date", ConditionOperator.Equals, "2026-01-02"), Values(("environment.date", now)), UtcPlus8));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(
            new ConditionClause("environment.time", ConditionOperator.LessThan, "01:00"), Values(("environment.time", now)), UtcPlus8));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(
            new ConditionClause("environment.date", ConditionOperator.Equals, "2026-01-02"), Values(("environment.date", now))));
    }

    /// <summary>
    /// 不带偏移的日期时间条件值按编译时区的墙钟解释，带偏移的照原偏移
    /// </summary>
    [Fact]
    public async Task DateTime_ConditionValueOffsets()
    {
        var instant = Values(("environment.now", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(
            new ConditionClause("environment.now", ConditionOperator.Equals, "2026-01-01T08:00:00"), instant, UtcPlus8));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(
            new ConditionClause("environment.now", ConditionOperator.Equals, "2026-01-01T08:00:00+08:00"), instant));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(
            new ConditionClause("environment.now", ConditionOperator.Equals, "2026-01-01T00:00:00Z"), instant, UtcPlus8));
    }

    /// <summary>
    /// 布尔认 true / false / 1 / 0
    /// </summary>
    [Fact]
    public async Task Boolean_Equals()
    {
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(
            new ConditionClause("subject.isImpersonating", ConditionOperator.Equals, "1"), Values(("subject.isImpersonating", true))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(
            new ConditionClause("subject.isImpersonating", ConditionOperator.Equals, "true"), Values(("subject.isImpersonating", "false"))));
    }

    /// <summary>
    /// 取反只翻转命中与不命中
    /// </summary>
    [Fact]
    public async Task Negation_FlipsOnlyDecidedOutcomes()
    {
        var name = Values(("subject.userName", "admin"));

        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.Equals, "admin", IsNegated: true), name));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.NotEquals, "admin", IsNegated: true), name));
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.Equals, "admin", IsNegated: true), Values()));
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(new ConditionClause("environment.ip", ConditionOperator.In, "10.0.0.0/8", IsNegated: true), Values()));
    }

    /// <summary>
    /// 为空：取不到、null、空串、空列表都算空；取反照常翻转
    /// </summary>
    [Fact]
    public async Task IsNull_TreatsMissingAndEmptyAsNull()
    {
        var isNull = new ConditionClause("subject.userName", ConditionOperator.IsNull, null);

        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(isNull, Values()));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(isNull, Values(("subject.userName", null))));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(isNull, Values(("subject.userName", "  "))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(isNull, Values(("subject.userName", "admin"))));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.IsNotNull, null), Values(("subject.userName", "admin"))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(isNull with { IsNegated = true }, Values()));
    }

    /// <summary>
    /// 属性值转换不了、子句编译出错都是无法判定
    /// </summary>
    [Fact]
    public async Task Indeterminate_OnConversionFailureOrCompileError()
    {
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(
            new ConditionClause("resource.amount", ConditionOperator.LessThan, "10"), Values(("resource.amount", "abc"))));
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(
            new ConditionClause("subject.unknown", ConditionOperator.Equals, "x"), Values(("subject.unknown", "x"))));
    }

    /// <summary>
    /// 组：全部命中才命中；有不命中或无法判定就不命中，哪怕还有待定；只剩待定时待定
    /// </summary>
    [Fact]
    public async Task Group_Outcomes()
    {
        var deferred = Values(("subject.userName", "admin"), ("resource.amount", ConditionAttributeValue.Deferred));

        Assert.Equal(ConditionOutcome.Pending, await EvaluateSetAsync(deferred,
            [new ConditionClause("subject.userName", ConditionOperator.Equals, "admin"), new ConditionClause("resource.amount", ConditionOperator.LessThan, "10")]));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateSetAsync(deferred,
            [new ConditionClause("resource.amount", ConditionOperator.LessThan, "10"), new ConditionClause("subject.userName", ConditionOperator.Equals, "guest")]));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateSetAsync(deferred,
            [new ConditionClause("resource.amount", ConditionOperator.LessThan, "10"), new ConditionClause("environment.ip", ConditionOperator.IsNotNull, null)]));
    }

    /// <summary>
    /// 条件集：任一组命中即命中；否则有待定即待定；否则不命中；空集恒命中
    /// </summary>
    [Fact]
    public async Task Set_Outcomes()
    {
        var values = Values(("subject.userName", "admin"), ("resource.amount", ConditionAttributeValue.Deferred));
        var matched = new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "admin")]);
        var pending = new ConditionGroup([new ConditionClause("resource.amount", ConditionOperator.LessThan, "10")]);
        var notMatched = new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "guest")]);

        Assert.Equal(ConditionOutcome.Matched, await EvaluateSetAsync(values, new ConditionSet([notMatched, pending, matched])));
        Assert.Equal(ConditionOutcome.Pending, await EvaluateSetAsync(values, new ConditionSet([notMatched, pending])));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateSetAsync(values, new ConditionSet([notMatched])));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateSetAsync(values, ConditionSet.Empty));
    }

    /// <summary>
    /// 不要求说明时短路：组内遇到不命中就不再取后面的属性，命中一组就不再看后面的组
    /// </summary>
    [Fact]
    public async Task ShortCircuit_WithoutExplain()
    {
        var source = new CountingSource(Values(("subject.userName", "admin"), ("resource.amount", 5)));
        var set = Compiler.Compile(new ConditionSet(
        [
            new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "guest"), new ConditionClause("resource.amount", ConditionOperator.LessThan, "10")]),
            new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "admin")]),
            new ConditionGroup([new ConditionClause("environment.ip", ConditionOperator.IsNull, null)])
        ]));

        var result = await Evaluator.EvaluateAsync(set, source);

        Assert.True(result.IsMatched);
        Assert.Empty(result.Groups);
        Assert.Equal(["subject.userName", "subject.userName"], source.Requested);
    }

    /// <summary>
    /// 要求说明时每组每个子句都给出结果、取到的值与原因
    /// </summary>
    [Fact]
    public async Task Explain_ReportsEveryClause()
    {
        var set = Compiler.Compile(new ConditionSet(
        [
            new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "admin", Key: "c1"), new ConditionClause("environment.ip", ConditionOperator.In, "10.0.0.0/8", Key: "c2")], "g1"),
            new ConditionGroup([new ConditionClause("resource.amount", ConditionOperator.LessThan, "10", Key: "c3")], "g2")
        ]));

        var result = await Evaluator.EvaluateAsync(set, new DictionaryConditionAttributeSource(Values(("subject.userName", "admin"), ("resource.amount", ConditionAttributeValue.Deferred))), explain: true);

        Assert.Equal(ConditionOutcome.Pending, result.Outcome);
        Assert.Equal(["g1", "g2"], result.Groups.Select(group => group.Key));
        Assert.Equal(ConditionOutcome.NotMatched, result.Groups[0].Outcome);
        Assert.Equal(["admin"], result.Groups[0].Clauses[0].AttributeValues);
        Assert.Equal(ConditionOutcome.Indeterminate, result.Groups[0].Clauses[1].Outcome);
        Assert.Equal("取不到属性值", result.Groups[0].Clauses[1].Message);
        Assert.Equal("c3", result.Groups[1].Clauses[0].Key);
        Assert.Equal(ConditionOutcome.Pending, result.Groups[1].Clauses[0].Outcome);
    }

    /// <summary>
    /// 令牌已取消时直接抛出
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_Canceled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Evaluator.EvaluateAsync(
            Compiler.Compile(ConditionSet.Empty), new DictionaryConditionAttributeSource(Values()), cancellationToken: cancellation.Token));
    }

    private static async Task<ConditionOutcome> EvaluateSetAsync(Dictionary<string, object?> values, IReadOnlyList<ConditionClause> clauses)
    {
        return await EvaluateSetAsync(values, new ConditionSet([new ConditionGroup(clauses)]));
    }

    private static async Task<ConditionOutcome> EvaluateSetAsync(Dictionary<string, object?> values, ConditionSet set)
    {
        var compiled = Compiler.Compile(set);
        var shortCircuited = await Evaluator.EvaluateAsync(compiled, new DictionaryConditionAttributeSource(values));
        var explained = await Evaluator.EvaluateAsync(compiled, new DictionaryConditionAttributeSource(values), explain: true);
        Assert.Equal(shortCircuited.Outcome, explained.Outcome);
        return shortCircuited.Outcome;
    }

    /// <summary>
    /// 记录被取过哪些属性的来源
    /// </summary>
    private sealed class CountingSource(Dictionary<string, object?> values) : IConditionAttributeSource
    {
        private readonly DictionaryConditionAttributeSource _inner = new(values);

        public List<string> Requested { get; } = [];

        public ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken = default)
        {
            Requested.Add(canonicalName);
            return _inner.GetAsync(canonicalName, cancellationToken);
        }
    }
}
