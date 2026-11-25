// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing.Workspace;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>expression-functions.json</c>：表达式的内置函数
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」的「内置函数」「时间」（ADR-0287、ADR-0296）：17 个内置函数的结果与错误，数字文本与时间文本合法与不合法的写法，<c>now()</c> 与 <c>date()</c> 的规范文本。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ExpressionFunctionVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ExpressionVectorReplay.ExpressionCases _vectors = SpecVectors.Read<ExpressionVectorReplay.ExpressionCases>("workflow", "expression-functions.json");

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
        // 17 个内置函数都被求值成功的用例调用过
        Assert.All<string>(["len", "contains", "startsWith", "endsWith", "upper", "lower", "trim", "isNullOrEmpty", "abs", "min", "max", "round", "format", "toNumber", "toString", "now", "date"], static function =>
            Assert.Contains(_vectors.Values, vector => vector.Expression.Contains(function + "(", StringComparison.OrdinalIgnoreCase)));

        // 时刻是文件里给的那个：now() 的结果就是它
        Assert.NotNull(_vectors.Now);
        Assert.Contains(_vectors.Values, static vector => vector.Expression == "now()" && vector.Text == _vectors.Now);

        // 不是数字、不是时间、放不下的错误都有
        Assert.All<string>([WorkflowMessages.ExpressionOperandsNotNumeric, WorkflowMessages.ExpressionNotDate, WorkflowMessages.ExpressionNumberOutOfRange], static key =>
            Assert.Contains(_vectors.Errors, vector => vector.MessageKey == key));

        // format（ADR-0296）：结果是带结尾的零的字符串，零位小数不写小数点，小数位数不合法是 number_out_of_range
        Assert.Contains(_vectors.Values, static vector => vector.Expression.StartsWith("format(", StringComparison.Ordinal) && vector.Type == "string" && vector.Text.EndsWith(".00", StringComparison.Ordinal));
        Assert.Contains(_vectors.Values, static vector => vector.Expression.StartsWith("format(", StringComparison.Ordinal) && vector.Expression.EndsWith(", 0)", StringComparison.Ordinal) && !vector.Text.Contains('.'));
        Assert.Contains(_vectors.Errors, static vector => vector.MessageKey == WorkflowMessages.ExpressionNumberOutOfRange && vector.Arguments["operator"]?.GetValue<string>() == "format");

        // upper / lower 是简单大小写映射（ADR-0303）：不变文化不转换的 U+0130 与 U+0131、平面以外的字母、没有简单映射而不变的字符都要有用例
        Assert.Contains(_vectors.Values, static vector => vector.Expression == "lower('İ')" && vector.Text == "i");
        Assert.Contains(_vectors.Values, static vector => vector.Expression == "upper('ı')" && vector.Text == "I");
        Assert.Contains(_vectors.Values, static vector => vector.Expression.StartsWith("upper(", StringComparison.Ordinal) && vector.Expression.Any(char.IsSurrogate) && vector.Text.Any(char.IsSurrogate) && !vector.Expression.Contains(vector.Text, StringComparison.Ordinal));
        Assert.Contains(_vectors.Values, static vector => vector.Expression.StartsWith("upper(", StringComparison.Ordinal) && vector.Text.Contains('ß'));
    }

    [Theory]
    [InlineData(1.5, 2, "1.50")]
    [InlineData(-1, 0, "-1")]
    [InlineData(0, 3, "0.000")]
    [InlineData(0.0001, 4, "0.0001")]
    public void FixedText_PadsTheCanonicalText(double number, int places, string expected)
        => Assert.Equal(expected, WorkflowNumberText.Format((decimal)number, places));

    [Fact]
    public void FixedText_RejectsANumberWithMoreDecimalsThanAsked()
    {
        // 调用方先舍入：位数多出来是编程错误，不悄悄截断
        Assert.Throws<ArgumentOutOfRangeException>(static () => WorkflowNumberText.Format(1.25m, 1));
        Assert.Throws<ArgumentOutOfRangeException>(static () => WorkflowNumberText.Format(1m, 29));
        Assert.Throws<ArgumentOutOfRangeException>(static () => WorkflowNumberText.Format(1m, -1));
    }
}
