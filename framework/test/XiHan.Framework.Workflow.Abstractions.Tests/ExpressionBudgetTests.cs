// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 一次求值的工作量上限（RFC-0021「表达式」的「长度与上限」，ADR-0296、ADR-0303）
/// </summary>
/// <remarks>
/// 计数规则逐条由 <c>expression-limits.json</c> 的 <c>budgets</c> 回放；这里核对向量写不出来的部分：几十万个值的容器、溢出的乘积、工作量按求值与渲染分开计、进程内由 CLR 类型构造的数字与写进存储再读回来的读取量相同、表达式得出的数字写成 JSON 是数字的文本
/// </remarks>
public sealed class ExpressionBudgetTests
{
    /// <summary>
    /// 被测求值器
    /// </summary>
    private static readonly WorkflowExpressionEvaluator _evaluator = new(new WorkflowOptions());

    [Fact]
    public void ComparingHugeArrays_HitsTheBudget()
    {
        // ADR-0287 报告的写法：两个百万元素的数组比相等，在 && 链里重复几百次，以前要算几分钟
        var scope = Scope(("a", Numbers(1_000_000)), ("b", Numbers(1_000_000)));
        var expression = "a == b" + string.Concat(Enumerable.Repeat(" && a == b", 300));

        var error = _evaluator.Evaluate(expression, scope, out var work).Error;

        Assert.Equal((WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionBudgetExceeded), (error?.Code, error?.MessageKey));
        Assert.Equal(ExpressionRun.MaxWork, error?.Details?["max"]);

        // 量到超过即停：记下的只比上限多出最后一个值，不是两个数组的规模
        Assert.InRange(work, ExpressionRun.MaxWork + 1, ExpressionRun.MaxWork + ExpressionRun.StepWork);
    }

    [Fact]
    public void FindingALongValueInAnArray_ChargesItOncePerElement()
    {
        // 1000 个元素各与一个百万码元的字符串比一次：一次的读取量就超过剩下额度的千分之一，不必乘出来
        var scope = Scope(("items", Numbers(1000)), ("k", new string('1', 1_048_576)));

        var error = _evaluator.Evaluate("contains(items, k)", scope, out var work).Error;

        Assert.Equal(WorkflowMessages.ExpressionBudgetExceeded, error?.MessageKey);
        Assert.True(work > ExpressionRun.MaxWork);

        // 元素少到乘起来不超过时照常
        Assert.True(_evaluator.Evaluate("contains(few, k)", Scope(("few", Numbers(15)), ("k", new string('1', 1_048_576))), out var some).ValueOrThrow().HasValue);
        // 三步，数组的 16 个值与 15 个数字 1 的写法，再按 15 个元素各读一次字符串
        Assert.Equal((3 * ExpressionRun.StepWork) + (16 * ExpressionRun.StepWork) + 15 + (15L * 1_048_576), some);
    }

    [Fact]
    public void EveryEvaluation_HasItsOwnBudget_AndARenderSharesOne()
    {
        // 拼到拼接上限的一次是 9437696：同一个作用域上分别求值两次都成功，同一个模板里两个占位就超过
        var scope = Scope(("k", new string('a', 1_048_576)));
        Assert.True(_evaluator.Evaluate("len(k + k + k + k)", scope).IsSuccess);
        Assert.True(_evaluator.Evaluate("len(k + k + k + k)", scope).IsSuccess);
        Assert.True(_evaluator.EvaluateCondition("len(k + k + k + k) > 0", scope).IsSuccess);

        var rendered = _evaluator.Render("{{ len(k + k + k + k) }} {{ len(k + k + k + k) }}", scope);
        Assert.Equal(WorkflowMessages.ExpressionBudgetExceeded, rendered.Error?.MessageKey);
        Assert.Equal("len(k + k + k + k)", rendered.Error?.Details?["expression"]);
    }

    [Fact]
    public void ContainersDeeperThanTheTextLimit_CountAsOneValue()
    {
        // 进程内才造得出超过 64 层的容器：与写成文本一样只算一个值，量的时候递归有界
        JsonNode deep = new JsonArray();
        for (var level = 0; level < 200; level++)
        {
            deep = new JsonArray(deep);
        }

        Assert.True(_evaluator.Evaluate("toString(deep)", Scope(("deep", deep)), out var work).IsSuccess);
        // 两步（toString 与变量），容器算到第 65 层为止各一个值
        Assert.Equal((2 + 65L) * ExpressionRun.StepWork, work);
    }

    [Fact]
    public void ReadingALongNumber_ChargesItsTextEveryTime()
    {
        // 所有者报告的写法：变量里原文几百万位的数字，每读一次都要把原文重新扫描一遍，以前计量是零（ADR-0303）
        var digits = 4_000_000;
        var scope = Scope(("n", JsonNode.Parse("1." + new string('0', digits - 2))));

        Assert.True(_evaluator.Evaluate("n + 0", scope, out var once).IsSuccess);
        Assert.Equal((3 * ExpressionRun.StepWork) + digits + 1, once);

        // 长度以内重复读几百次：读到第五次就超过上限，不再随表达式的写法放大
        var expression = "n" + string.Concat(Enumerable.Repeat(" + n", 500));
        var error = _evaluator.Evaluate(expression, scope, out var work).Error;
        Assert.Equal(WorkflowMessages.ExpressionBudgetExceeded, error?.MessageKey);
        Assert.InRange(work, ExpressionRun.MaxWork + 1, ExpressionRun.MaxWork + digits);
    }

    [Theory]
    [MemberData(nameof(ClrNumbers))]
    public void ClrNumbers_ChargeTheTextTheyAreWrittenAs_LikeAfterStorage(JsonNode number)
    {
        // 进程内由 CLR 类型构造的数字按它写成 JSON 的文本记：写进存储再读回来的原文就是这段文本，两次的工作量相同
        var stored = JsonNode.Parse(number.ToJsonString());

        Assert.True(_evaluator.Evaluate("x == x", Scope(("x", number)), out var inProcess).IsSuccess);
        Assert.True(_evaluator.Evaluate("x == x", Scope(("x", stored)), out var afterStorage).IsSuccess);

        Assert.Equal(afterStorage, inProcess);
        Assert.Equal((3 * ExpressionRun.StepWork) + (2L * number.ToJsonString().Length), inProcess);
    }

    [Theory]
    [InlineData("1.5 + 1.5", "3")]
    [InlineData("2.50 * 2", "5")]
    [InlineData("1.50", "1.5")]
    [InlineData("-0.0 * 1", "0")]
    [InlineData("toNumber('1.50')", "1.5")]
    [InlineData("round(2.50, 1)", "2.5")]
    [InlineData("min(1.0, 2)", "1")]
    [InlineData("0.1 + 0.2", "0.3")]
    public void ComputedNumbers_AreWrittenAsTheirNumberText(string expression, string json)
    {
        // 表达式得出的数字不带 decimal 内部的小数位数：存进变量写成 JSON 就是数字的文本（ADR-0303）
        var result = _evaluator.Evaluate(expression, Scope()).ValueOrThrow().OrNull();
        Assert.Equal(json, result?.ToJsonString());

        // 存进变量、读回来再读一次，工作量与没存之前相同
        Assert.True(_evaluator.Evaluate("x == x", Scope(("x", result?.DeepClone())), out var before).IsSuccess);
        Assert.True(_evaluator.Evaluate("x == x", Scope(("x", JsonNode.Parse(result!.ToJsonString()))), out var after).IsSuccess);
        Assert.Equal(before, after);
    }

    [Fact]
    public void LenAndIsNullOrEmpty_ReadNumbers_ButNotStrings()
    {
        var scope = Scope(("n", JsonNode.Parse("12.50")), ("s", "12.50"));

        Assert.True(_evaluator.Evaluate("len(n)", scope, out var number).IsSuccess);
        Assert.True(_evaluator.Evaluate("len(s)", scope, out var text).IsSuccess);
        Assert.True(_evaluator.Evaluate("isNullOrEmpty(n)", scope, out var empty).IsSuccess);

        // 数字的文本要先从写法 12.50 读出值才写得出 12.5；字符串的长度不用读内容
        Assert.Equal((2 * ExpressionRun.StepWork) + 5, number);
        Assert.Equal(2 * ExpressionRun.StepWork, text);
        Assert.Equal((2 * ExpressionRun.StepWork) + 5, empty);
    }

    /// <summary>
    /// 进程内由各种 CLR 类型构造的数字
    /// </summary>
    public static TheoryData<JsonNode> ClrNumbers() =>
    [
        JsonValue.Create(1.50m),
        JsonValue.Create(0m),
        JsonValue.Create(-0.0m),
        JsonValue.Create(79228162514264337593543950335m),
        JsonValue.Create(0.0000000000000000000000000001m),
        JsonValue.Create(42),
        JsonValue.Create(-7L),
        JsonValue.Create(0.1),
        JsonValue.Create(1e20),
        JsonValue.Create(1.5f),
        JsonValue.Create((short)300),
        JsonValue.Create(ulong.MaxValue),
    ];

    /// <summary>
    /// 有这些变量的作用域
    /// </summary>
    private static ExpressionScope Scope(params (string Name, JsonNode? Value)[] variables)
    {
        var obj = new JsonObject();
        foreach (var (name, value) in variables)
        {
            obj[name] = value;
        }

        return new ExpressionScope(obj, TimeProvider.System);
    }

    /// <summary>
    /// 指定个数的数字 1 组成的数组
    /// </summary>
    private static JsonArray Numbers(int count)
    {
        var array = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            array.Add(1);
        }

        return array;
    }
}
