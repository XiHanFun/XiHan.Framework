// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 表达式里原先与平台绑定或会逃出求值器的行为
/// </summary>
/// <remarks>
/// RFC-0021「表达式」，ADR-0287。每个用例复现一处修正之前的行为：溢出与越界的异常逃出求值器，数字的文本带 decimal 内部的小数位数，容器的文本是 System.Text.Json 的缺省写法，时间交给平台的日期解析（只有时间的文本补上机器当天的日期），除法先求值右操作数，渲染的结果没有上限，错误里带出的文本不截断或切开代理对
/// </remarks>
public sealed class ExpressionPortabilityTests
{
    /// <summary>
    /// 默认选项的求值器
    /// </summary>
    private static readonly WorkflowExpressionEvaluator _evaluator = new(new WorkflowOptions());

    /// <summary>
    /// 固定时钟：2026-10-26 08:00:00.1234567 UTC
    /// </summary>
    private static readonly DateTimeOffset _now = new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero).AddTicks(1234567);

    [Theory]
    [InlineData("79228162514264337593543950335 + 1", "+")]
    [InlineData("-79228162514264337593543950335 - 1", "-")]
    [InlineData("79228162514264337593543950335 * 2", "*")]
    [InlineData("79228162514264337593543950335 / 0.1", "/")]
    [InlineData("round(2.5, -1)", "round")]
    [InlineData("round(2.5, 29)", "round")]
    [InlineData("round(2.5, 99999999999)", "round")]
    public void NumbersOutOfRange_AreExpressionErrors_NotExceptions(string expression, string op)
    {
        // 修正之前这里抛出 OverflowException 或 ArgumentOutOfRangeException，逃出返回 Result 的求值器
        var error = _evaluator.Evaluate(expression, Scope()).Error;

        Assert.Equal(WorkflowErrors.ExpressionInvalid, error?.Code);
        Assert.Equal(WorkflowMessages.ExpressionNumberOutOfRange, error?.MessageKey);
        Assert.Equal(op, error?.Details?["operator"]);
    }

    [Theory]
    [InlineData("items[99999999999]")]
    [InlineData("items[-99999999999]")]
    [InlineData("items[79228162514264337593543950335]")]
    public void AnIndexBeyondTheIntegerRange_IsOutOfRange_NotAnException(string expression)
    {
        var error = _evaluator.Evaluate(expression, Scope()).Error;

        Assert.Equal(WorkflowMessages.ExpressionIndexOutOfRange, error?.MessageKey);
        Assert.Equal(3, error?.Details?["length"]);
    }

    [Theory]
    [InlineData("1.50", "1.5")]
    [InlineData("1.5 + 1.5", "3")]
    [InlineData("2.50 * 2", "5")]
    [InlineData("6.00 / 2.0", "3")]
    [InlineData("price", "19.9")]
    [InlineData("price * 2", "39.8")]
    [InlineData("-0.0", "0")]
    [InlineData("min(1, 1.0)", "1")]
    [InlineData("min(1.0, 1)", "1")]
    [InlineData("1 / 3", "0.3333333333333333333333333333")]
    public void TheTextOfANumber_DependsOnItsValueOnly(string expression, string text)
    {
        // 修正之前照 decimal 内部的小数位数写出：1.50、3.0、5.00、19.90，min(1, 1.0) 与 min(1.0, 1) 写出不同的文本
        var scope = Scope();

        Assert.Equal(text, _evaluator.Render("{{ " + expression + " }}", scope).ValueOrThrow());
        Assert.Equal(text, Text("toString(" + expression + ")", scope));
        Assert.Equal(text, Text("'' + (" + expression + ")", scope));
    }

    [Fact]
    public void TheTextOfAContainer_IsCompactJson_WithSortedMembersAndPlainCharacters()
    {
        // 修正之前是 System.Text.Json 的缺省写法：非 ASCII 字符与 < > & ' + " 写成 \uXXXX，数字照读入时的原文，成员照原来的顺序
        var scope = Scope();

        Assert.Equal("[\"请假\",\"<b>&'+\",1.5,100]", Text("toString(mixed)", scope));
        Assert.Equal("{\"a\":[\"请假\",\"<b>&'+\",1.5,100],\"b\":\"q\\\"s\\\\\\n\",\"键\":null}", _evaluator.Render("{{ record }}", scope).ValueOrThrow());
    }

    [Fact]
    public void Now_AndDate_AreStrings_ThatStayTheSameThroughStorage()
    {
        // 修正之前是进程内的时间值：文本带七位小数，写进变量、过一遍存储之后成了不带多余小数位的字符串
        var scope = Scope();
        var now = _evaluator.Evaluate("now()", scope).ValueOrThrow().ValueOrThrow();

        Assert.Equal(JsonValueKind.String, now.GetValueKind());
        Assert.Equal("2026-10-26T08:00:00.1234567+00:00", now.GetValue<string>());
        Assert.Equal("2026-01-01T19:04:05.0000000+00:00", Text("date('2026-01-02T03:04:05+08:00')", scope));

        // 过一遍存储：写成 JSON 再读回来，值与它的文本都不变
        var stored = new JsonObject { ["at"] = JsonNode.Parse(now.ToJsonString()) };
        var later = new ExpressionScope(stored, new FakeTimeProvider(_now));
        Assert.True(_evaluator.EvaluateCondition("at == now() && toString(at) == toString(now())", later).ValueOrThrow());
    }

    [Theory]
    [InlineData("12:30")]
    [InlineData("01/02/2026")]
    [InlineData("Jan 2, 2026")]
    [InlineData("2026-1-2")]
    [InlineData("2026-01")]
    [InlineData(" 2026-01-02 ")]
    [InlineData("2026-01-02T03:04:05.1234567890Z")]
    public void Date_ReadsTheGrammarOnly(string text)
    {
        // 修正之前交给平台的日期解析：这些写法都读得出，只有时间的补上机器当天的日期
        var error = _evaluator.Evaluate("date(value)", Scope().With("value", JsonValue.Create(text))).Error;

        Assert.Equal(WorkflowMessages.ExpressionNotDate, error?.MessageKey);
        Assert.Equal(text, error?.Details?["value"]);
    }

    [Theory]
    [InlineData("'1/2' < '1/10'", false)]
    [InlineData("'9:30' < '10:45'", false)]
    [InlineData("'2026-01-02T00:00:00+08:00' < '2026-01-01T20:00:00Z'", true)]
    [InlineData("'2026-01-02 03:04' < '2026-01-02T03:04:01Z'", true)]
    public void Comparison_TreatsOnlyTheTimeGrammarAsTime(string expression, bool holds)
    {
        // 修正之前前两条按平台读出的日期与时间比，结果随机器的日期变；按文法它们不是时间，按文本的序数比
        Assert.Equal(holds, _evaluator.EvaluateCondition(expression, Scope()).ValueOrThrow());
    }

    [Theory]
    [InlineData("'x' / 0", "workflow.expression.operands_not_numeric")]
    [InlineData("nope / 0", "workflow.expression.unknown_variable")]
    [InlineData("'x' % 0", "workflow.expression.operands_not_numeric")]
    [InlineData("'x' - nope", "workflow.expression.unknown_variable")]
    [InlineData("n * nope", "workflow.expression.unknown_variable")]
    [InlineData("round('x', nope)", "workflow.expression.unknown_variable")]
    [InlineData("min('x', nope)", "workflow.expression.unknown_variable")]
    [InlineData("1 / 0", "workflow.expression.division_by_zero")]
    public void Operands_AreEvaluatedLeftToRight_BeforeAnyCheck(string expression, string messageKey)
    {
        // 修正之前 / 与 % 先求值右操作数并先看除数（前三条都报除以零），- 与 * 核对完左操作数才求值右操作数，round 先求值第二个实参
        Assert.Equal(messageKey, _evaluator.Evaluate(expression, Scope()).Error?.MessageKey);
    }

    [Fact]
    public void NumberTexts_FollowTheGrammar_NotThePlatformParser()
    {
        var scope = Scope();

        // 修正之前结尾带 NUL 字符的字符串也读得出
        Assert.Equal(WorkflowMessages.ExpressionOperandsNotNumeric, _evaluator.Evaluate("toNumber(value)", Scope().With("value", JsonValue.Create("5\0"))).Error?.MessageKey);

        // 修正之前位数与指数都上千时读出的不是数学值：这里是 1 后面两千个零再乘 10 的负两千次方，就是 1
        var digits = "1" + new string('0', 2000) + "e-2000";
        Assert.Equal("1", Text("toString(toNumber(value))", Scope().With("value", JsonValue.Create(digits))));

        // 变量里的 JSON 数字按原文的数学值：写法不同的同一个数没有区别
        Assert.True(_evaluator.EvaluateCondition("price == 19.9 && toString(price) == '19.9' && hundred == 100 && toString(hundred) == '100'", scope).ValueOrThrow());

        // 容器里的数字同样按放进数字之后的数值比：超出 28 位小数的两种写法舍入成同一个数
        var precise = (JsonObject)JsonNode.Parse("{\"a\": [0.10000000000000000000000000001, 1e2], \"b\": [0.1, 100.0]}")!;
        Assert.True(_evaluator.EvaluateCondition("a == b && a[0] == b[0]", new ExpressionScope(precise, new FakeTimeProvider(_now))).ValueOrThrow());
    }

    [Fact]
    public void ValuesBuiltFromClrTypes_BehaveLikeTheirJsonForm()
    {
        // 还没有过存储的值按它写成 JSON 之后的形式参与求值：过不过存储结果相同
        var sum = 0.1 + 0.2;
        var variables = new JsonObject
        {
            ["fromDouble"] = JsonValue.Create(sum),
            ["fromFloat"] = JsonValue.Create(0.5f),
            ["fromShort"] = JsonValue.Create((short)7),
            ["at"] = JsonValue.Create(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8))),
        };
        var stored = (JsonObject)JsonNode.Parse(variables.ToJsonString())!;
        const string Expression = "toString(fromDouble) + '|' + (fromFloat + fromShort) + '|' + at + '|' + date(at)";

        var before = Text(Expression, new ExpressionScope(variables, new FakeTimeProvider(_now)));
        var after = Text(Expression, new ExpressionScope(stored, new FakeTimeProvider(_now)));

        Assert.Equal("0.30000000000000004|7.5|2026-01-02T03:04:05+08:00|2026-01-01T19:04:05.0000000+00:00", after);
        Assert.Equal(after, before);
    }

    [Fact]
    public void ARenderedTemplate_HasTheSameLengthLimitAsConcatenation()
    {
        // 修正之前渲染的结果没有上限：占位可以是整个变量的文本，一个不长的模板能渲染出几个 GB
        var scope = Scope().With("block", JsonValue.Create(new string('x', 1024 * 1024)));

        Assert.Equal(ExpressionCompiler.MaxStringLength, _evaluator.Render("{{block}}{{block}}{{block}}{{block}}", scope).ValueOrThrow().Length);

        var error = _evaluator.Render("{{block}}{{block}}{{block}}{{block}}!", scope).Error;
        Assert.Equal(WorkflowErrors.ExpressionInvalid, error?.Code);
        Assert.Equal(WorkflowMessages.ExpressionStringTooLong, error?.MessageKey);
        Assert.Equal((long)ExpressionCompiler.MaxStringLength + 1, error?.Details?["length"]);
        Assert.Equal(ExpressionCompiler.MaxStringLength, error?.Details?["max"]);
        Assert.Equal("{{block}}{{block}}{{block}}{{block}}!", error?.Details?["expression"]);
    }

    [Fact]
    public void EchoedTexts_AreBounded_AndNeverSplitASurrogatePair()
    {
        var scope = Scope();
        var padding = new string('a', 198);

        // 第 200 个码元是代理对的前一半：只取 199 个，不留下半个字符
        var split = (string)_evaluator.Evaluate("'" + padding + "😀' + nope", scope).Error!.Details!["expression"]!;
        Assert.Equal("'" + padding + "…", split);
        Assert.DoesNotContain(split, static character => char.IsSurrogate(character));

        // 修正之前这三处不截断：不是时间的文本、条件的结果不是布尔、模板的占位没有结束
        var long300 = new string('x', 300);
        Assert.Equal(new string('x', 200) + "…", _evaluator.Evaluate("date(value)", Scope().With("value", JsonValue.Create(long300))).Error!.Details!["value"]);
        Assert.Equal(201, ((string)_evaluator.EvaluateCondition("1" + new string(' ', 300), scope).Error!.Details!["expression"]!).Length);
        Assert.Equal(new string('x', 200) + "…", _evaluator.Render(long300 + "{{ price", scope).Error!.Details!["expression"]);
    }

    [Fact]
    public void AnUnexpectedCharacterOutsideTheBasicPlane_IsReportedWhole()
    {
        // 修正之前只带出代理对的前一半
        var error = _evaluator.Validate("1 + 😀").Error;

        Assert.Equal(WorkflowMessages.ExpressionUnexpectedCharacter, error?.MessageKey);
        Assert.Equal("😀", error?.Details?["character"]);
        Assert.Equal(5, error?.Details?["position"]);
    }

    [Fact]
    public void MemberAndVariableNames_AreCaseSensitive_EvenOnACaseInsensitiveObject()
    {
        // 修正之前名字区不区分大小写取决于对象是怎样造出来的：按 Web 缺省选项反序列化出来的不区分，过一遍存储之后又区分
        var loose = new JsonNodeOptions { PropertyNameCaseInsensitive = true };
        var variables = new JsonObject(loose) { ["amount"] = 5, ["order"] = new JsonObject(loose) { ["id"] = "A1" }, ["same"] = new JsonObject(loose) { ["id"] = "A1" } };
        var scope = new ExpressionScope(variables, new FakeTimeProvider(_now));

        Assert.Equal(WorkflowMessages.ExpressionUnknownVariable, _evaluator.Evaluate("Amount", scope).Error?.MessageKey);
        Assert.False(_evaluator.Evaluate("order.ID", scope).ValueOrThrow().HasValue);
        Assert.False(_evaluator.Evaluate("order['Id']", scope).ValueOrThrow().HasValue);
        Assert.Equal("A1", Text("order.id", scope));
        Assert.True(_evaluator.EvaluateCondition("order == same", scope).ValueOrThrow());
    }

    [Fact]
    public void NumberText_AndTimeText_RoundTripTheirCanonicalForms()
    {
        Assert.True(WorkflowNumberText.TryParse(" +12.50e1 ", out var number));
        Assert.Equal("125", WorkflowNumberText.Format(number));
        Assert.Equal("0", WorkflowNumberText.Format(-0.0m));
        Assert.False(WorkflowNumberText.TryParse("1e29", out _));
        Assert.True(WorkflowNumberText.TryParse("1e-29", out var tiny) && tiny == 0);

        Assert.True(WorkflowTimeText.TryParse("2026-01-02 03:04", out var time));
        Assert.Equal("2026-01-02T03:04:00.0000000+00:00", WorkflowTimeText.Format(time));
        Assert.True(WorkflowTimeText.TryParse(WorkflowTimeText.Format(_now), out var again) && again == _now);
        Assert.False(WorkflowTimeText.TryParse("2026-01-02T03:04.5Z", out _));
    }

    /// <summary>
    /// 作用域：几个变量与固定的时钟
    /// </summary>
    private static ExpressionScope Scope()
    {
        var variables = (JsonObject)JsonNode.Parse("""
            {
              "items": [1, 2, 3],
              "price": 19.90,
              "hundred": 1e2,
              "n": null,
              "mixed": ["请假", "<b>&'+", 1.50, 1e2],
              "record": { "键": null, "b": "q\"s\\\n", "a": ["请假", "<b>&'+", 1.50, 1e2] }
            }
            """)!;
        return new ExpressionScope(variables, new FakeTimeProvider(_now));
    }

    /// <summary>
    /// 求值成字符串
    /// </summary>
    private static string Text(string expression, ExpressionScope scope)
    {
        Result<Maybe<JsonNode>> evaluated = _evaluator.Evaluate(expression, scope);
        return evaluated.ValueOrThrow().ValueOrThrow().GetValue<string>();
    }
}
