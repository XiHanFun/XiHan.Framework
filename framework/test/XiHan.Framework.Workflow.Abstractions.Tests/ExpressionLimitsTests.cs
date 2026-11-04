// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Expressions;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 表达式的长度与嵌套上限（RFC-0021「表达式」、ADR-0120）
/// </summary>
/// <remarks>
/// 表达式由持流程定义权限的用户写。解析是递归下降，栈溢出接不住，进程直接退出，所以这里真的喂几千层：没有上限时这个测试进程会崩
/// </remarks>
public sealed class ExpressionLimitsTests
{
    /// <summary>
    /// 超长的消息键
    /// </summary>
    private const string TooLong = "workflow.expression.too_long";

    /// <summary>
    /// 嵌套过深的消息键
    /// </summary>
    private const string TooDeep = "workflow.expression.too_deep";

    /// <summary>
    /// 被测求值器
    /// </summary>
    private static readonly WorkflowExpressionEvaluator _evaluator = new(new WorkflowOptions());

    [Fact]
    public void ThreeThousandParentheses_AreRejected_WithANormalError()
    {
        var expression = Repeat("(", 3000) + "1" + Repeat(")", 3000);

        var error = _evaluator.Validate(expression).Error;

        Assert.Equal(WorkflowErrors.ExpressionInvalid, error?.Code);
        Assert.Equal(TooLong, error?.MessageKey);
        Assert.Equal(expression.Length, error?.Details?["length"]);
        Assert.Equal(4000, error?.Details?["max"]);
    }

    [Theory]
    [InlineData("(", "1", ")", 1900)]
    [InlineData("!", "true", "", 3000)]
    [InlineData("-", "1", "", 3000)]
    [InlineData("- ", "1", "", 1900)]
    [InlineData("abs(", "1", ")", 780)]
    [InlineData("x[", "0", "]", 1300)]
    [InlineData("(!", "true", ")", 1300)]
    [InlineData("max(1,", "1", ")", 550)]
    public void DeepNesting_WithinTheLengthLimit_IsRejected_OnEveryRecursivePath(string open, string leaf, string close, int levels)
    {
        var expression = Repeat(open, levels) + leaf + Repeat(close, levels);
        Assert.True(expression.Length <= 4000, "The expression has to reach the depth limit, not the length limit.");

        var error = _evaluator.Validate(expression).Error;

        Assert.Equal(WorkflowErrors.ExpressionInvalid, error?.Code);
        Assert.Equal(TooDeep, error?.MessageKey);
        Assert.Equal(64, error?.Details?["max"]);
    }

    [Theory]
    [InlineData("(", "1", ")")]
    [InlineData("!", "true", "")]
    [InlineData("-", "1", "")]
    [InlineData("abs(", "1", ")")]
    [InlineData("x[", "0", "]")]
    [InlineData("min(1,", "1", ")")]
    public void DepthLimit_Boundary_SixtyFourPasses_SixtyFiveFails(string open, string leaf, string close)
    {
        var deepest = Repeat(open, 64) + leaf + Repeat(close, 64);
        var beyond = Repeat(open, 65) + leaf + Repeat(close, 65);

        Assert.True(_evaluator.Validate(deepest).IsSuccess);
        Assert.Equal(TooDeep, _evaluator.Validate(beyond).Error?.MessageKey);
    }

    [Fact]
    public void Depth_CountsEveryPathTogether()
    {
        // 每种写法交替，合起来 66 层：单算哪一种都没超
        var open = new StringBuilder();
        var close = new StringBuilder();
        for (var level = 0; level < 22; level++)
        {
            open.Append("(abs(-");
            close.Insert(0, "))");
        }

        Assert.Equal(TooDeep, _evaluator.Validate(open + "1" + close).Error?.MessageKey);
    }

    [Fact]
    public void Depth_IsNesting_NotAPerExpressionCount()
    {
        // 并列的括号和函数不累加：层数出来就退回去
        var siblings = string.Join(" + ", Enumerable.Repeat("(abs(1))", 100));

        Assert.Equal(100, EvaluateNumber(siblings));
    }

    [Fact]
    public void LengthLimit_Boundary_CountsWhitespaceToo()
    {
        var atLimit = "1" + new string(' ', 3999);
        var beyond = "1" + new string(' ', 4000);

        Assert.True(_evaluator.Validate(atLimit).IsSuccess);
        Assert.Equal(TooLong, _evaluator.Validate(beyond).Error?.MessageKey);
    }

    [Fact]
    public void LongestChains_StillEvaluate_WithoutOverflowingTheStack()
    {
        // 链不是嵌套，解析是循环，不受层数限制；但求值时闭包是一层套一层的，长度上限把它压在两千层左右
        var additions = string.Join("+", Enumerable.Repeat("1", 2000));
        var members = "null" + string.Concat(Enumerable.Repeat(".x", 1998));
        var both = "true" + string.Concat(Enumerable.Repeat("||true", 660));
        Assert.True(additions.Length <= 4000 && members.Length <= 4000 && both.Length <= 4000);

        Assert.Equal(2000, EvaluateNumber(additions));
        Assert.True(_evaluator.Evaluate(members, EmptyScope()).IsSuccess);
        Assert.True(_evaluator.EvaluateCondition(both, EmptyScope()).ValueOrThrow());
    }

    [Fact]
    public void Error_DoesNotEchoAHugeExpression()
    {
        // 错误会进校验结果、响应和实例故障：超长的表达式不能原样带出去
        var error = _evaluator.Validate(new string('1', 1_000_000)).Error;

        Assert.Equal(TooLong, error?.MessageKey);
        Assert.True(error!.Message.Length < 1000, $"The message is {error.Message.Length} characters long.");
        Assert.True(((string?)error.Details?["expression"])?.Length < 300);
    }

    [Fact]
    public void Template_WithADeepPlaceholder_ReturnsTheError()
    {
        var template = "{{ " + Repeat("(", 1900) + "1" + Repeat(")", 1900) + " }}";

        Assert.Equal(TooDeep, _evaluator.Render(template, EmptyScope()).Error?.MessageKey);
    }

    /// <summary>
    /// 没有变量的作用域
    /// </summary>
    private static ExpressionScope EmptyScope() => new(new JsonObject(), TimeProvider.System);

    /// <summary>
    /// 重复文本
    /// </summary>
    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    /// <summary>
    /// 求值成数字
    /// </summary>
    private static decimal EvaluateNumber(string expression)
        => _evaluator.Evaluate(expression, EmptyScope()).ValueOrThrow().ValueOrThrow().GetValue<decimal>();
}
