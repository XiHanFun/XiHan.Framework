// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>expression-syntax.json</c>：表达式的词法与文法
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」的「词法」「文法」（ADR-0287）：数字与字符串字面量、空白、标识符、优先级与结合性、成员与索引与调用的写法，解析时的每一种错误与它的位置。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ExpressionSyntaxVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ExpressionVectorReplay.ExpressionCases _vectors = SpecVectors.Read<ExpressionVectorReplay.ExpressionCases>("workflow", "expression-syntax.json");

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
        // 防止向量换了写法后某一类一条都没核对还照样通过：解析时的错误与求值时的错误都要有，位置要有不在开头的
        Assert.Contains(_vectors.Errors, static vector => vector.Syntax);
        Assert.Contains(_vectors.Errors, static vector => !vector.Syntax);
        Assert.Contains(_vectors.Errors, static vector => vector.Arguments["position"] is { } position && position.GetValue<int>() > 1);

        // 六种结果里词法能直接写出的四种，以及非 ASCII 的空白与标识符（不带单引号的表达式里的非 ASCII 字母只能是标识符）
        Assert.All<string>(["null", "boolean", "number", "string"], static type => Assert.Contains(_vectors.Values, vector => vector.Type == type));
        Assert.Contains(_vectors.Values, static vector => vector.Expression.Any(static character => character > 127 && char.IsWhiteSpace(character)));
        Assert.Contains(_vectors.Values, static vector => vector.Expression.Any(static character => character > 127 && char.IsLetter(character)) && !vector.Expression.Contains((char)39));
    }
}
