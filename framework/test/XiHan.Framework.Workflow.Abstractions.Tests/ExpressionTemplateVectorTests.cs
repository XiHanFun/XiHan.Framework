// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>expression-templates.json</c>：模板的渲染与表达式当条件求值
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」的「模板」「条件」（ADR-0287）：<c>{{ 表达式 }}</c> 占位的查找（引号里的 <c>}}</c> 不算结束）、结果按值的文本写出、从左到右遇到第一个错误即停；条件的结果必须是布尔。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ExpressionTemplateVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TemplateVectors _vectors = ConformanceVectors.Load<TemplateVectors>("expression-templates.json");

    [Fact]
    public void Renders_ProduceTheTextInTheVectors()
    {
        Assert.NotEmpty(_vectors.Renders);
        var failures = new List<string>();
        foreach (var vector in _vectors.Renders)
        {
            var rendered = ExpressionVectorReplay.Evaluator.Render(vector.Template, ExpressionVectorReplay.Scope(_vectors.Variables, _vectors.Now));
            if (rendered.Error is { } error)
            {
                failures.Add($"{vector.Name}: '{vector.Template}' fails with '{error.MessageKey}', the vector has '{vector.Text}'.");
            }
            else if (!string.Equals(rendered.ValueOrThrow(), vector.Text, StringComparison.Ordinal))
            {
                failures.Add($"{vector.Name}: '{vector.Template}' renders '{rendered.ValueOrThrow()}', the vector has '{vector.Text}'.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void RenderErrors_FailWithTheMessageKeyAndArgumentsInTheVectors()
    {
        Assert.NotEmpty(_vectors.RenderErrors);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _vectors.ErrorCode);
        var failures = new List<string>();
        foreach (var vector in _vectors.RenderErrors)
        {
            var rendered = ExpressionVectorReplay.Evaluator.Render(vector.Template, ExpressionVectorReplay.Scope(_vectors.Variables, _vectors.Now));
            if (ExpressionVectorReplay.Mismatch(rendered.Error, _vectors.ErrorCode, vector.MessageKey, vector.Arguments, vector.Expression) is { } mismatch)
            {
                failures.Add($"{vector.Name}: '{vector.Template}' {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Conditions_HoldAsInTheVectors()
    {
        Assert.NotEmpty(_vectors.Conditions);
        var failures = new List<string>();
        foreach (var vector in _vectors.Conditions)
        {
            var condition = ExpressionVectorReplay.Evaluator.EvaluateCondition(vector.Expression, ExpressionVectorReplay.Scope(_vectors.Variables, _vectors.Now));
            if (condition.Error is { } error)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' fails with '{error.MessageKey}', the vector has {vector.Holds}.");
            }
            else if (condition.ValueOrThrow() != vector.Holds)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' is {condition.ValueOrThrow()}, the vector has {vector.Holds}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ConditionErrors_FailWithTheMessageKeyAndArgumentsInTheVectors()
    {
        Assert.NotEmpty(_vectors.ConditionErrors);
        var failures = new List<string>();
        foreach (var vector in _vectors.ConditionErrors)
        {
            var condition = ExpressionVectorReplay.Evaluator.EvaluateCondition(vector.Expression, ExpressionVectorReplay.Scope(_vectors.Variables, _vectors.Now));
            if (ExpressionVectorReplay.Mismatch(condition.Error, _vectors.ErrorCode, vector.MessageKey, vector.Arguments, vector.Expression) is { } mismatch)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' {mismatch}");
            }

            // 解析时就报出的条件，只校验语法也失败；其余的语法合法
            if (ExpressionVectorReplay.Evaluator.Validate(vector.Expression).IsFailure != vector.Syntax)
            {
                failures.Add($"{vector.Name}: validating '{vector.Expression}' must {(vector.Syntax ? "fail" : "succeed")}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_KeepTheCasesThatTellTheRulesApart()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：没有占位的、引号里带 }} 的、结果是容器的模板都要有
        Assert.Contains(_vectors.Renders, static vector => !vector.Template.Contains("{{", StringComparison.Ordinal));
        Assert.Contains(_vectors.Renders, static vector => vector.Template.Contains("'}}'", StringComparison.Ordinal));
        Assert.Contains(_vectors.Renders, static vector => vector.Text.StartsWith('[') && vector.Text.EndsWith(']'));
        Assert.Contains(_vectors.Renders, static vector => vector.Text.StartsWith('{') && vector.Text.EndsWith('}') && vector.Template.StartsWith("{{", StringComparison.Ordinal) && !vector.Text.StartsWith("{{", StringComparison.Ordinal));

        // 占位没有结束的、占位里求值失败的都要有；占位里失败时带出的是占位里的文本，不是整个模板
        Assert.Contains(_vectors.RenderErrors, static vector => vector.MessageKey == WorkflowMessages.ExpressionUnterminatedPlaceholder && vector.Expression == vector.Template);
        Assert.Contains(_vectors.RenderErrors, static vector => vector.MessageKey != WorkflowMessages.ExpressionUnterminatedPlaceholder && vector.Expression != vector.Template);

        // 条件：两种结果、不是布尔的、求值失败的
        Assert.Contains(_vectors.Conditions, static vector => vector.Holds);
        Assert.Contains(_vectors.Conditions, static vector => !vector.Holds);
        Assert.Contains(_vectors.ConditionErrors, static vector => vector.MessageKey == WorkflowMessages.ExpressionNotBoolean);
        Assert.Contains(_vectors.ConditionErrors, static vector => vector.MessageKey != WorkflowMessages.ExpressionNotBoolean);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="ErrorCode">失败用例的错误码</param>
    /// <param name="Now">求值时的时刻</param>
    /// <param name="Variables">实例变量</param>
    /// <param name="Renders">渲染成功的用例</param>
    /// <param name="RenderErrors">渲染失败的用例</param>
    /// <param name="Conditions">条件的用例</param>
    /// <param name="ConditionErrors">条件失败的用例</param>
    private sealed record TemplateVectors(string ErrorCode, string Now, JsonObject Variables, RenderCase[] Renders, RenderErrorCase[] RenderErrors, ConditionCase[] Conditions, ExpressionVectorReplay.ErrorCase[] ConditionErrors);

    /// <summary>
    /// 一条渲染成功的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Template">模板</param>
    /// <param name="Text">渲染出的文本</param>
    private sealed record RenderCase(string Name, string Template, string Text);

    /// <summary>
    /// 一条渲染失败的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Template">模板</param>
    /// <param name="MessageKey">消息键</param>
    /// <param name="Expression">错误的 <c>expression</c> 参数</param>
    /// <param name="Arguments">其余的参数</param>
    private sealed record RenderErrorCase(string Name, string Template, string MessageKey, string Expression, JsonObject Arguments);

    /// <summary>
    /// 一条条件用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Expression">表达式</param>
    /// <param name="Holds">条件是否成立</param>
    private sealed record ConditionCase(string Name, string Expression, bool Holds);
}
