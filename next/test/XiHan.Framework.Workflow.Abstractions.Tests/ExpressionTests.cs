// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 表达式语言：算术、比较、逻辑短路、字符串与函数、成员与下标、模板渲染、校验与错误。
/// </summary>
public sealed class ExpressionTests
{
    /// <summary>
    /// 默认选项的求值器。
    /// </summary>
    private static readonly WorkflowExpressionEvaluator _evaluator = new(new WorkflowOptions());

    /// <summary>
    /// 固定时钟：2026-10-26 08:00 UTC。
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ArithmeticComparisonAndLogic()
    {
        var scope = Scope(new JsonObject { ["x"] = 5, ["amount"] = 500, ["flag"] = false, ["name"] = "bob" });
        Assert.Equal(7m, Number("1 + 2 * 3", scope));
        Assert.Equal(9m, Number("(1 + 2) * 3", scope));
        Assert.Equal(2.5m, Number("10 / 4", scope));
        Assert.Equal(-5m, Number("-x", scope));
        Assert.True(Bool("amount > 100 && amount <= 500", scope));
        Assert.True(Bool("x >= 5 && x != 6 && x == 5", scope));
        Assert.True(Bool("name == 'bob' && name != 'alice'", scope));
        Assert.True(Bool("!flag", scope));
        Assert.False(Bool("flag || x < 1", scope));
        // 短路：右侧引用未知变量也不求值
        Assert.True(Bool("true || nope > 1", scope));
        Assert.False(Bool("false && nope > 1", scope));
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Evaluate("false || nope > 1", scope).Error?.Code);
    }

    [Fact]
    public void Evaluate_StringsFunctionsMembersAndIndexes()
    {
        var clock = new FakeTimeProvider(_now);
        var scope = Scope(new JsonObject
        {
            ["name"] = " Bob ",
            ["items"] = new JsonArray(1, 2, 3),
            ["lastError"] = new JsonObject { ["code"] = "WORKFLOW_UNEXPECTED_001" },
            ["empty"] = "",
        }, clock);
        Assert.Equal("hi  BOB ", Text("'hi ' + upper(name)", scope));
        Assert.Equal("bob", Text("lower(trim(name))", scope));
        Assert.Equal(5m, Number("len(name)", scope));
        Assert.Equal(3m, Number("len(items)", scope));
        Assert.True(Bool("contains(name, 'ob') && contains(items, 2) && !contains(items, 9)", scope));
        Assert.True(Bool("startsWith(trim(name), 'B') && endsWith(trim(name), 'b')", scope));
        Assert.True(Bool("isNullOrEmpty(empty) && !isNullOrEmpty(items)", scope));
        Assert.Equal(3m, Number("abs(-3)", scope));
        Assert.Equal(2m, Number("min(2, 5)", scope));
        Assert.Equal(5m, Number("max(2, 5)", scope));
        Assert.Equal(2.57m, Number("round(2.567, 2)", scope));
        Assert.Equal(3m, Number("round(2.5)", scope));
        Assert.Equal(43m, Number("toNumber('42') + 1", scope));
        Assert.Equal("5x", Text("toString(5) + 'x'", scope));
        Assert.True(Bool("lastError.code == 'WORKFLOW_UNEXPECTED_001'", scope));
        Assert.True(Bool("lastError.missing == null", scope));
        Assert.Equal(1m, Number("items[0]", scope));
        Assert.Equal(3m, Number("items[1 + 1]", scope));
        Assert.Equal(_now, _evaluator.Evaluate("now()", scope).ValueOrThrow()!.GetValue<DateTimeOffset>());
        Assert.True(Bool("date('2026-01-02') > date('2026-01-01') && now() > date('2026-10-25')", scope));
    }

    [Fact]
    public void Render_ReplacesPlaceholders_AndRejectsUnterminated()
    {
        var scope = Scope(new JsonObject { ["name"] = "bob", ["amount"] = 42, ["tags"] = new JsonArray("a") });
        Assert.Equal("Hello BOB, total 84 [\"a\"] done", _evaluator.Render("Hello {{ upper(name) }}, total {{ amount * 2 }} {{ tags }} done", scope).ValueOrThrow());
        Assert.Equal("plain", _evaluator.Render("plain", scope).ValueOrThrow());
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Render("oops {{ name", scope).Error?.Code);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Render("{{ nope }}", scope).Error?.Code);
    }

    [Fact]
    public void Validate_And_EvaluateCondition_ReportErrors()
    {
        Assert.True(_evaluator.Validate("x > 1 && contains(list, 'a')").IsSuccess);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Validate("1 +").Error?.Code);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Validate("   ").Error?.Code);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Validate("unknownFn(1)").Error?.Code);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Validate("len()").Error?.Code);

        var scope = Scope(new JsonObject { ["x"] = 2, ["s"] = "text" });
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.EvaluateCondition("1 + 1", scope).Error?.Code);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Evaluate("-s", scope).Error?.Code);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _evaluator.Evaluate("x && true", scope).Error?.Code);
        Assert.True(_evaluator.EvaluateCondition("x > 1", scope).ValueOrThrow());
    }

    [Fact]
    public void Cache_IsBounded_AndStaysCorrect()
    {
        var evaluator = new WorkflowExpressionEvaluator(new WorkflowOptions { ExpressionCacheSize = 1 });
        var scope = Scope([]);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(2m, evaluator.Evaluate("1 + 1", scope).ValueOrThrow()!.GetValue<decimal>());
            Assert.Equal(4m, evaluator.Evaluate("2 + 2", scope).ValueOrThrow()!.GetValue<decimal>());
        }

        // With 的临时值覆盖同名变量，不改变量本身
        var variables = new JsonObject { ["outcome"] = "a" };
        Assert.Equal("b", Text("outcome", new ExpressionScope(variables, TimeProvider.System).With("outcome", "b")));
        Assert.Equal("a", variables["outcome"]!.GetValue<string>());
    }

    /// <summary>
    /// 作用域。
    /// </summary>
    private static ExpressionScope Scope(JsonObject variables, TimeProvider? clock = null) => new(variables, clock ?? TimeProvider.System);

    /// <summary>
    /// 求值成数字。
    /// </summary>
    private static decimal Number(string expression, ExpressionScope scope) => WorkflowValues.TryConvert<decimal>(_evaluator.Evaluate(expression, scope).ValueOrThrow(), out var number) ? number : throw new InvalidOperationException("Not a number.");

    /// <summary>
    /// 求值成字符串。
    /// </summary>
    private static string Text(string expression, ExpressionScope scope) => _evaluator.Evaluate(expression, scope).ValueOrThrow()!.GetValue<string>();

    /// <summary>
    /// 求值成条件。
    /// </summary>
    private static bool Bool(string expression, ExpressionScope scope) => _evaluator.EvaluateCondition(expression, scope).ValueOrThrow();
}
