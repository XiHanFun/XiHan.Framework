// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>expression-functions.json</c>：表达式的内置函数
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」的「内置函数」「时间」（ADR-0287）：16 个内置函数的结果与错误，数字文本与时间文本合法与不合法的写法，<c>now()</c> 与 <c>date()</c> 的规范文本。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ExpressionFunctionVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ExpressionVectorReplay.ExpressionCases _vectors = ConformanceVectors.Load<ExpressionVectorReplay.ExpressionCases>("expression-functions.json");

    [Fact]
    public void Values_EvaluateToTheTypeAndTextInTheVectors()
    {
        Assert.NotEmpty(_vectors.Values);
        var failures = new List<string>();
        ExpressionVectorReplay.ReplayValues(_vectors, failures);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Errors_FailWithTheMessageKeyAndArgumentsInTheVectors()
    {
        Assert.NotEmpty(_vectors.Errors);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _vectors.ErrorCode);
        var failures = new List<string>();
        ExpressionVectorReplay.ReplayErrors(_vectors, failures);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_KeepTheCasesThatTellTheRulesApart()
    {
        // 16 个内置函数都被求值成功的用例调用过
        Assert.All<string>(["len", "contains", "startsWith", "endsWith", "upper", "lower", "trim", "isNullOrEmpty", "abs", "min", "max", "round", "toNumber", "toString", "now", "date"], static function =>
            Assert.Contains(_vectors.Values, vector => vector.Expression.Contains(function + "(", StringComparison.OrdinalIgnoreCase)));

        // 时刻是文件里给的那个：now() 的结果就是它
        Assert.NotNull(_vectors.Now);
        Assert.Contains(_vectors.Values, static vector => vector.Expression == "now()" && vector.Text == _vectors.Now);

        // 不是数字、不是时间、放不下的错误都有
        Assert.All<string>([WorkflowMessages.ExpressionOperandsNotNumeric, WorkflowMessages.ExpressionNotDate, WorkflowMessages.ExpressionNumberOutOfRange], static key =>
            Assert.Contains(_vectors.Errors, vector => vector.MessageKey == key));
    }
}
