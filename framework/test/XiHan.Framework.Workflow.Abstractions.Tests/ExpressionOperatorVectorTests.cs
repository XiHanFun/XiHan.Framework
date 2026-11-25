// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>expression-operators.json</c>：表达式的运算符、成员与索引
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」的「值」「运算符」「成员与索引」（ADR-0287）：每个运算符与每种操作数的组合，数字的舍入与范围，数字文本，除以零与求值的先后，拼接，相等，比较，逻辑的短路，成员与索引。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ExpressionOperatorVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ExpressionVectorReplay.ExpressionCases _vectors = SpecVectors.Read<ExpressionVectorReplay.ExpressionCases>("workflow", "expression-operators.json");

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
        // 每个运算符在成功与失败的用例里都出现过；相等与不等对任何操作数都有结果，没有失败的用例
        Assert.All<string>([" + ", " - ", " * ", " / ", " % ", " == ", " != ", " < ", " <= ", " > ", " >= ", " && ", " || "], static op =>
            Assert.Contains(_vectors.Values, vector => vector.Expression.Contains(op, StringComparison.Ordinal)));
        Assert.All<string>([" + ", " - ", " * ", " / ", " % ", " < ", " <= ", " > ", " >= ", " && ", " || "], static op =>
            Assert.Contains(_vectors.Errors, vector => vector.Expression.Contains(op, StringComparison.Ordinal)));

        // 六种结果都有；放不下的、除以零的、越界的错误都有
        Assert.All<string>(["null", "boolean", "number", "string", "array", "object"], static type => Assert.Contains(_vectors.Values, vector => vector.Type == type));
        Assert.All<string>([WorkflowMessages.ExpressionNumberOutOfRange, WorkflowMessages.ExpressionDivisionByZero, WorkflowMessages.ExpressionIndexOutOfRange, WorkflowMessages.ExpressionOperandsNotScalar], static key =>
            Assert.Contains(_vectors.Errors, vector => vector.MessageKey == key));

        // 这个文件里的错误都是求值时才报出的
        Assert.DoesNotContain(_vectors.Errors, static vector => vector.Syntax);
    }
}
