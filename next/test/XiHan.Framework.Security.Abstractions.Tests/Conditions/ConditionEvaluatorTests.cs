// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using XiHan.Framework.Security.Abstractions.Conditions;
using static XiHan.Framework.Security.Abstractions.Tests.Fixtures.ConditionTestKit;

namespace XiHan.Framework.Security.Abstractions.Tests.Conditions;

/// <summary>
/// 条件求值器。
/// </summary>
/// <remarks>
/// 锁死三条容易写错的语义——取不到值是无法判定（按不命中计且取反不翻转）；待定让组待定，但组里只要有不命中就不命中；条件集任一组命中即命中，否则有待定即待定。
/// </remarks>
public sealed class ConditionEvaluatorTests
{
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
    public async Task String_Operators_IgnoreCase(ConditionOperator @operator, string value, ConditionOutcome expected)
        => Assert.Equal(expected, await EvaluateAsync(new ConditionClause("subject.userName", @operator, value), Values(("subject.userName", "admin"))));

    [Theory]
    [InlineData(ConditionOperator.GreaterThan, "9999.5", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.GreaterThanOrEquals, "10000", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.LessThan, "10000", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.LessThanOrEquals, "10000.00", ConditionOutcome.Matched)]
    [InlineData(ConditionOperator.Between, "9,18", ConditionOutcome.NotMatched)]
    [InlineData(ConditionOperator.Between, "{\"min\":10000,\"max\":20000}", ConditionOutcome.Matched)]
    public async Task Number_Operators_AcceptAnyNumericTypeOrText(ConditionOperator @operator, string value, ConditionOutcome expected)
    {
        Assert.Equal(expected, await EvaluateAsync(new ConditionClause("resource.amount", @operator, value), Values(("resource.amount", 10000))));
        Assert.Equal(expected, await EvaluateAsync(new ConditionClause("resource.amount", @operator, value), Values(("resource.amount", "10000"))));
    }

    [Fact]
    public async Task MultiValued_AnyElement_AndEmptyListIsPresent()
    {
        var roles = Values(("subject.roleCodes", new List<string> { "employee", "Auditor" }));
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

    [Fact]
    public async Task MultiValued_Numbers()
    {
        var levels = Values(("subject.levels", new object[] { 1, 3L, 5.0m }));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.levels", ConditionOperator.Contains, "3"), levels));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.levels", ConditionOperator.GreaterThan, "4"), levels));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.levels", ConditionOperator.GreaterThan, "5"), levels));
    }

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

    [Fact]
    public async Task Ip_Equals_NormalizesMappedAddresses()
    {
        var clause = new ConditionClause("environment.ip", ConditionOperator.Equals, "203.0.113.9");
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(clause, Values(("environment.ip", IPAddress.Parse("::ffff:203.0.113.9")))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(clause, Values(("environment.ip", "203.0.113.10"))));
    }

    [Theory]
    [InlineData("23:30", ConditionOutcome.Matched)]
    [InlineData("06:00", ConditionOutcome.Matched)]
    [InlineData("22:00", ConditionOutcome.Matched)]
    [InlineData("12:00", ConditionOutcome.NotMatched)]
    public async Task TimeOfDay_BetweenAcrossMidnight(string time, ConditionOutcome expected)
        => Assert.Equal(expected, await EvaluateAsync(new ConditionClause("environment.time", ConditionOperator.Between, "22:00,06:00"), Values(("environment.time", TimeOnly.Parse(time, CultureInfo.InvariantCulture)))));

    [Fact]
    public async Task DateAndTime_UseCompileTimeZone()
    {
        // UTC 16:30 在东八区已是次日 00:30
        var now = new DateTimeOffset(2026, 1, 1, 16, 30, 0, TimeSpan.Zero);
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("environment.date", ConditionOperator.Equals, "2026-01-02"), Values(("environment.date", now)), UtcPlus8));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("environment.time", ConditionOperator.LessThan, "01:00"), Values(("environment.time", now)), UtcPlus8));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("environment.date", ConditionOperator.Equals, "2026-01-02"), Values(("environment.date", now))));
    }

    [Fact]
    public async Task DateTime_ConditionValuesWithoutOffset_UseCompileTimeZone()
    {
        var instant = Values(("environment.now", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("environment.now", ConditionOperator.Equals, "2026-01-01T08:00:00"), instant, UtcPlus8));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("environment.now", ConditionOperator.Equals, "2026-01-01T08:00:00+08:00"), instant));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("environment.now", ConditionOperator.Equals, "2026-01-01T00:00:00Z"), instant, UtcPlus8));
    }

    [Fact]
    public async Task Boolean_AcceptsTrueFalseOneZero()
    {
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.isImpersonating", ConditionOperator.Equals, "1"), Values(("subject.isImpersonating", true))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.isImpersonating", ConditionOperator.Equals, "true"), Values(("subject.isImpersonating", "false"))));
    }

    [Fact]
    public async Task Negation_FlipsOnlyDecidedOutcomes()
    {
        var name = Values(("subject.userName", "admin"));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.Equals, "admin", IsNegated: true), name));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.NotEquals, "admin", IsNegated: true), name));
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.Equals, "admin", IsNegated: true), Values()));
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(new ConditionClause("environment.ip", ConditionOperator.In, "10.0.0.0/8", IsNegated: true), Values()));
    }

    [Fact]
    public async Task IsNull_TreatsMissingAndEmptyAsNull_AndMayBeNegated()
    {
        var isNull = new ConditionClause("subject.userName", ConditionOperator.IsNull, null);
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(isNull, Values()));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(isNull, Values(("subject.userName", null))));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(isNull, Values(("subject.userName", "  "))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(isNull, Values(("subject.userName", "admin"))));
        Assert.Equal(ConditionOutcome.Matched, await EvaluateAsync(new ConditionClause("subject.userName", ConditionOperator.IsNotNull, null), Values(("subject.userName", "admin"))));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateAsync(isNull with { IsNegated = true }, Values()));
    }

    [Fact]
    public async Task Indeterminate_OnConversionFailureOrCompileError()
    {
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(new ConditionClause("resource.amount", ConditionOperator.LessThan, "10"), Values(("resource.amount", "abc"))));
        Assert.Equal(ConditionOutcome.Indeterminate, await EvaluateAsync(new ConditionClause("subject.unknown", ConditionOperator.Equals, "x"), Values(("subject.unknown", "x"))));
    }

    [Fact]
    public async Task Group_AllMustMatch_FailureBeatsPending()
    {
        var deferred = Values(("subject.userName", "admin"), ("resource.amount", ConditionAttributeValue.Deferred));
        Assert.Equal(ConditionOutcome.Pending, await EvaluateSetAsync(deferred, [new ConditionClause("subject.userName", ConditionOperator.Equals, "admin"), new ConditionClause("resource.amount", ConditionOperator.LessThan, "10")]));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateSetAsync(deferred, [new ConditionClause("resource.amount", ConditionOperator.LessThan, "10"), new ConditionClause("subject.userName", ConditionOperator.Equals, "guest")]));
        Assert.Equal(ConditionOutcome.NotMatched, await EvaluateSetAsync(deferred, [new ConditionClause("resource.amount", ConditionOperator.LessThan, "10"), new ConditionClause("environment.ip", ConditionOperator.IsNotNull, null)]));
    }

    [Fact]
    public async Task Set_AnyGroupMatches_ElsePending_ElseNotMatched()
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

    [Fact]
    public async Task WithoutExplain_ShortCircuits()
    {
        var requested = new List<string>();
        var inner = new DictionaryConditionAttributeSource(Values(("subject.userName", "admin"), ("resource.amount", 5)));
        var compiled = Compiler.Compile(new ConditionSet(
        [
            new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "guest"), new ConditionClause("resource.amount", ConditionOperator.LessThan, "10")]),
            new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "admin")]),
            new ConditionGroup([new ConditionClause("environment.ip", ConditionOperator.IsNull, null)]),
        ]));

        var result = await Evaluator.EvaluateAsync(compiled, new RecordingSource(inner, requested), explain: false, CancellationToken.None);

        Assert.True(result.IsMatched);
        Assert.Empty(result.Groups);
        Assert.Equal(["subject.userName", "subject.userName"], requested);
    }

    [Fact]
    public async Task Explain_ReportsEveryClause_WithLocalizableReasons()
    {
        var compiled = Compiler.Compile(new ConditionSet(
        [
            new ConditionGroup([new ConditionClause("subject.userName", ConditionOperator.Equals, "admin", Key: "c1"), new ConditionClause("environment.ip", ConditionOperator.In, "10.0.0.0/8", Key: "c2")], "g1"),
            new ConditionGroup([new ConditionClause("resource.amount", ConditionOperator.LessThan, "10", Key: "c3")], "g2"),
            new ConditionGroup([new ConditionClause("resource.amount", ConditionOperator.GreaterThan, "x", Key: "c4"), new ConditionClause("subject.levels", ConditionOperator.Contains, "1", Key: "c5")], "g3"),
        ]));

        var source = new DictionaryConditionAttributeSource(Values(("subject.userName", "admin"), ("resource.amount", ConditionAttributeValue.Deferred), ("subject.levels", new object[] { "one" })));
        var result = await Evaluator.EvaluateAsync(compiled, source, explain: true, CancellationToken.None);

        Assert.Equal(ConditionOutcome.Pending, result.Outcome);
        Assert.Equal(["g1", "g2", "g3"], result.Groups.Select(static group => group.Key));
        Assert.Equal(ConditionOutcome.NotMatched, result.Groups[0].Outcome);
        Assert.Equal(["admin"], result.Groups[0].Clauses[0].AttributeValues);
        Assert.Null(result.Groups[0].Clauses[0].Reason);
        var missing = result.Groups[0].Clauses[1];
        Assert.Equal((ConditionOutcome.Indeterminate, SecurityErrors.ConditionAttributeMissing, SecurityMessages.ConditionAttributeMissing), (missing.Outcome, missing.Reason?.Code, missing.Reason?.MessageKey));
        var pending = result.Groups[1].Clauses[0];
        Assert.Equal(("c3", ConditionOutcome.Pending, SecurityErrors.ConditionAttributePending), (pending.Key, pending.Outcome, pending.Reason?.Code));
        Assert.Equal((ConditionOutcome.Indeterminate, SecurityErrors.ConditionInvalid), (result.Groups[2].Clauses[0].Outcome, result.Groups[2].Clauses[0].Reason?.Code));
        var invalid = result.Groups[2].Clauses[1];
        Assert.Equal((ConditionOutcome.Indeterminate, SecurityErrors.ConditionAttributeInvalid, (object?)"one"), (invalid.Outcome, invalid.Reason?.Code, invalid.Reason?.Details?["value"]));
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Evaluator.EvaluateAsync(Compiler.Compile(ConditionSet.Empty), new DictionaryConditionAttributeSource(Values()), explain: false, cancellation.Token));
    }

    /// <summary>
    /// 不带说明与带说明各求一次，两者结果一致，返回结果。
    /// </summary>
    private static Task<ConditionOutcome> EvaluateSetAsync(Dictionary<string, object?> values, IReadOnlyList<ConditionClause> clauses) => EvaluateSetAsync(values, new ConditionSet([new ConditionGroup(clauses)]));

    /// <summary>
    /// 不带说明与带说明各求一次，两者结果一致，返回结果。
    /// </summary>
    private static async Task<ConditionOutcome> EvaluateSetAsync(Dictionary<string, object?> values, ConditionSet conditions)
    {
        var compiled = Compiler.Compile(conditions);
        var shortCircuited = await Evaluator.EvaluateAsync(compiled, new DictionaryConditionAttributeSource(values), explain: false, CancellationToken.None);
        var explained = await Evaluator.EvaluateAsync(compiled, new DictionaryConditionAttributeSource(values), explain: true, CancellationToken.None);
        Assert.Equal(shortCircuited.Outcome, explained.Outcome);
        return shortCircuited.Outcome;
    }

    /// <summary>
    /// 记录被取过哪些属性的来源。
    /// </summary>
    private sealed class RecordingSource(IConditionAttributeSource inner, List<string> requested) : IConditionAttributeSource
    {
        /// <inheritdoc />
        public ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken)
        {
            requested.Add(canonicalName);
            return inner.GetAsync(canonicalName, cancellationToken);
        }
    }
}
