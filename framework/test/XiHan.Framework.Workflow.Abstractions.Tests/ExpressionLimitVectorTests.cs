// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing.Workspace;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>expression-limits.json</c>：表达式的上限、错误里带出的文本与工作量
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」的「长度与上限」（ADR-0120、ADR-0182、ADR-0287、ADR-0296）：长度 4000、嵌套 64 层、拼接与渲染出的文本 4194304、错误里带出的文本 200、一次求值的工作量 16777216，长度都按 UTF-16 码元计。这个文件里的表达式由重复的片段拼出来。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ExpressionLimitVectorTests
{
    /// <summary>
    /// 表达式的五个向量文件
    /// </summary>
    private static readonly string[] _files = ["expression-syntax.json", "expression-operators.json", "expression-functions.json", "expression-templates.json", "expression-limits.json"];

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly LimitVectors _vectors = SpecVectors.Read<LimitVectors>("workflow", "expression-limits.json");

    /// <summary>
    /// 求值时的实例变量：文件里的变量加上几个很长的字符串与数组
    /// </summary>
    private static readonly JsonObject _variables = BuildVariables();

    [Fact]
    public void Limits_AreTheOnesInTheVectors()
    {
        Assert.Equal(WorkflowErrors.ExpressionInvalid, _vectors.ErrorCode);
        Assert.Equal(ExpressionCompiler.MaxLength, _vectors.MaxLength);
        Assert.Equal(ExpressionCompiler.MaxDepth, _vectors.MaxDepth);
        Assert.Equal(ExpressionCompiler.MaxStringLength, _vectors.MaxStringLength);
        Assert.Equal(_vectors.MaxEchoLength, ExpressionException.Echo(new string('a', _vectors.MaxEchoLength)).Length);
        Assert.Equal(_vectors.MaxEchoLength + 1, ExpressionException.Echo(new string('a', _vectors.MaxEchoLength + 1)).Length);
        Assert.Equal(ExpressionRun.MaxWork, _vectors.MaxWork);
        Assert.Equal(ExpressionRun.StepWork, _vectors.StepWork);
    }

    [Fact]
    public void Budgets_CountTheWork_AndFailBeyondTheLimit()
    {
        Assert.NotEmpty(_vectors.Budgets);
        var failures = new List<string>();
        foreach (var vector in _vectors.Budgets)
        {
            var source = vector.Head + Repeat(vector.Link, vector.Count) + vector.Tail;
            var scope = ExpressionVectorReplay.Scope(_variables);
            long work;
            var error = vector.Mode switch
            {
                "evaluate" => ExpressionVectorReplay.Evaluator.Evaluate(source, scope, out work).Error,
                "render" => ExpressionVectorReplay.Evaluator.Render(source, scope, out work).Error,
                _ => throw new InvalidOperationException($"Budget case '{vector.Name}' has the unknown mode '{vector.Mode}'."),
            };
            if (vector.Valid)
            {
                if (error is not null)
                {
                    failures.Add($"{vector.Name}: must succeed, fails with '{error.MessageKey}' after {work} units of work.");
                }
                else if (work != vector.Work)
                {
                    failures.Add($"{vector.Name}: counts {work} units of work, the vector has {vector.Work}.");
                }

                continue;
            }

            // 失败的用例核对全部参数：expression 是带出的文本，求值占位里的表达式时超过是那段表达式，读占位的结果时超过是模板
            var expected = new JsonObject { ["expression"] = vector.Expression, ["max"] = _vectors.MaxWork };
            if (error is null)
            {
                failures.Add($"{vector.Name}: succeeds after {work} units of work, the vector has '{WorkflowMessages.ExpressionBudgetExceeded}'.");
            }
            else if (error.Code != _vectors.ErrorCode || error.MessageKey != WorkflowMessages.ExpressionBudgetExceeded || !JsonNode.DeepEquals(ExpressionVectorReplay.ArgumentsOf(error), expected))
            {
                failures.Add($"{vector.Name}: fails with '{error.MessageKey}' {ExpressionVectorReplay.ArgumentsOf(error).ToJsonString()}, the vector has '{WorkflowMessages.ExpressionBudgetExceeded}' {expected.ToJsonString()}.");
            }
            else if (work <= _vectors.MaxWork)
            {
                failures.Add($"{vector.Name}: stops at {work} units of work, which is not over the limit.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Lengths_PassUpToTheLimit_AndFailBeyondIt()
    {
        Assert.NotEmpty(_vectors.Lengths);
        var failures = new List<string>();
        foreach (var vector in _vectors.Lengths)
        {
            var expression = vector.Head + Repeat(vector.Padding, vector.Count) + vector.Tail;
            if (expression.Length != vector.Length)
            {
                failures.Add($"{vector.Name}: the expression is {expression.Length} code units long, the vector has {vector.Length}.");
            }

            var validated = ExpressionVectorReplay.Evaluator.Validate(expression);
            if (vector.Valid)
            {
                if (validated.Error is { } error)
                {
                    failures.Add($"{vector.Name}: must be valid, validation fails with '{error.MessageKey}'.");
                }
            }
            else if (Mismatch(validated.Error, WorkflowMessages.ExpressionTooLong, new JsonObject { ["length"] = vector.Length, ["max"] = _vectors.MaxLength }) is { } mismatch)
            {
                failures.Add($"{vector.Name}: {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Nestings_PassUpToTheLimit_AndFailBeyondIt()
    {
        Assert.NotEmpty(_vectors.Nestings);
        var failures = new List<string>();
        foreach (var vector in _vectors.Nestings)
        {
            var expression = Repeat(vector.Open, vector.Levels) + vector.Leaf + Repeat(vector.Close, vector.Levels);
            var validated = ExpressionVectorReplay.Evaluator.Validate(expression);
            if (vector.Valid)
            {
                // 合法的不止能解析，还要能求值：解析通过而求值时撑爆栈的实现过不了这一条
                if (validated.Error is { } error)
                {
                    failures.Add($"{vector.Name}: must be valid, validation fails with '{error.MessageKey}'.");
                }
                else if (ExpressionVectorReplay.Evaluator.Evaluate(expression, ExpressionVectorReplay.Scope(_variables)).Error is { } runtime)
                {
                    failures.Add($"{vector.Name}: must evaluate, evaluation fails with '{runtime.MessageKey}'.");
                }
            }
            else if (Mismatch(validated.Error, WorkflowMessages.ExpressionTooDeep, new JsonObject { ["max"] = _vectors.MaxDepth }) is { } mismatch)
            {
                failures.Add($"{vector.Name}: {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Chains_AreNotNesting_AndEvaluate()
    {
        Assert.NotEmpty(_vectors.Chains);
        var failures = new List<string>();
        foreach (var vector in _vectors.Chains)
        {
            var expression = vector.Head + Repeat(vector.Link, vector.Count);
            var evaluated = ExpressionVectorReplay.Evaluator.Evaluate(expression, ExpressionVectorReplay.Scope(_variables));
            if (evaluated.Error is { } error)
            {
                failures.Add($"{vector.Name}: fails with '{error.MessageKey}', the vector has {vector.Type} '{vector.Text}'.");
                continue;
            }

            var node = evaluated.ValueOrThrow().OrNull();
            if (ExpressionVectorReplay.TypeOf(node) != vector.Type || WorkflowValueText.Of(node) != vector.Text)
            {
                failures.Add($"{vector.Name}: is {ExpressionVectorReplay.TypeOf(node)} '{WorkflowValueText.Of(node)}', the vector has {vector.Type} '{vector.Text}'.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Concatenations_PassUpToTheLimit_AndFailBeyondIt()
    {
        Assert.NotEmpty(_vectors.Concatenations);
        var failures = new List<string>();
        foreach (var vector in _vectors.Concatenations)
        {
            var evaluated = ExpressionVectorReplay.Evaluator.Evaluate(vector.Expression, ExpressionVectorReplay.Scope(_variables));
            if (vector.Valid)
            {
                if (evaluated.Error is { } error)
                {
                    failures.Add($"{vector.Name}: must succeed, fails with '{error.MessageKey}'.");
                }
                else if (evaluated.ValueOrThrow().OrNull()?.GetValue<string>().Length != vector.Length)
                {
                    failures.Add($"{vector.Name}: the result is not a string of {vector.Length} code units.");
                }
            }
            else if (Mismatch(evaluated.Error, WorkflowMessages.ExpressionStringTooLong, new JsonObject { ["length"] = vector.Length, ["max"] = _vectors.MaxStringLength }) is { } mismatch)
            {
                failures.Add($"{vector.Name}: {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Renderings_PassUpToTheLimit_AndFailBeyondIt()
    {
        Assert.NotEmpty(_vectors.Renderings);
        var failures = new List<string>();
        foreach (var vector in _vectors.Renderings)
        {
            var rendered = ExpressionVectorReplay.Evaluator.Render(vector.Template, ExpressionVectorReplay.Scope(_variables));
            if (vector.Valid)
            {
                if (rendered.Error is { } error)
                {
                    failures.Add($"{vector.Name}: must succeed, fails with '{error.MessageKey}'.");
                }
                else if (rendered.ValueOrThrow().Length != vector.Length)
                {
                    failures.Add($"{vector.Name}: the text is {rendered.ValueOrThrow().Length} code units long, the vector has {vector.Length}.");
                }
            }
            else if (Mismatch(rendered.Error, WorkflowMessages.ExpressionStringTooLong, new JsonObject { ["length"] = vector.Length, ["max"] = _vectors.MaxStringLength }) is { } mismatch)
            {
                failures.Add($"{vector.Name}: {mismatch}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Echoes_CarryTheTruncatedText()
    {
        Assert.NotEmpty(_vectors.Echoes);
        var failures = new List<string>();
        foreach (var vector in _vectors.Echoes)
        {
            var scope = ExpressionVectorReplay.Scope(_variables);
            var error = vector.Mode switch
            {
                "evaluate" => ExpressionVectorReplay.Evaluator.Evaluate(vector.Source, scope).Error,
                "condition" => ExpressionVectorReplay.Evaluator.EvaluateCondition(vector.Source, scope).Error,
                "render" => ExpressionVectorReplay.Evaluator.Render(vector.Source, scope).Error,
                _ => throw new InvalidOperationException($"Echo case '{vector.Name}' has the unknown mode '{vector.Mode}'."),
            };
            if (error is null)
            {
                failures.Add($"{vector.Name}: succeeds, the vector has '{vector.MessageKey}'.");
                continue;
            }

            // 这里的参数含 expression：核对的正是带出来的文本
            var arguments = ExpressionVectorReplay.ArgumentsOf(error);
            if (error.Code != _vectors.ErrorCode || error.MessageKey != vector.MessageKey || !JsonNode.DeepEquals(arguments, vector.Arguments))
            {
                failures.Add($"{vector.Name}: fails with '{error.MessageKey}' {arguments.ToJsonString()}, the vector has '{vector.MessageKey}' {vector.Arguments.ToJsonString()}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_KeepBothSidesOfEveryLimit()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：每个上限两侧都要有，还要有码位数不超而码元数超过的
        Assert.Contains(_vectors.Lengths, static vector => vector.Valid && vector.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Lengths, static vector => !vector.Valid && vector.Length == _vectors.MaxLength + 1);
        Assert.Contains(_vectors.Lengths, static vector => !vector.Valid && vector.Padding.Length == 2 && char.IsSurrogatePair(vector.Padding[0], vector.Padding[1]) && vector.Count + 2 <= _vectors.MaxLength);
        Assert.Contains(_vectors.Nestings, static vector => vector.Valid && vector.Levels == _vectors.MaxDepth);
        Assert.Contains(_vectors.Nestings, static vector => !vector.Valid && vector.Levels == _vectors.MaxDepth + 1);
        Assert.Contains(_vectors.Concatenations, static vector => vector.Valid && vector.Length == _vectors.MaxStringLength);
        Assert.Contains(_vectors.Concatenations, static vector => !vector.Valid && vector.Length == _vectors.MaxStringLength + 1);
        Assert.Contains(_vectors.Renderings, static vector => vector.Valid && vector.Length == _vectors.MaxStringLength);
        Assert.Contains(_vectors.Renderings, static vector => !vector.Valid && vector.Length == _vectors.MaxStringLength + 1);

        // 工作量：求值与渲染都有正好到上限的与超过的；读占位的结果时超过（带模板）与求值占位时超过（带那段表达式）都有
        Assert.All<string>(["evaluate", "render"], static mode =>
        {
            Assert.Contains(_vectors.Budgets, vector => vector.Mode == mode && vector.Valid && vector.Work == _vectors.MaxWork);
            Assert.Contains(_vectors.Budgets, vector => vector.Mode == mode && !vector.Valid);
        });
        Assert.Contains(_vectors.Budgets, static vector => vector.Mode == "render" && !vector.Valid && vector.Expression == ExpressionException.Echo(vector.Head + Repeat(vector.Link, vector.Count) + vector.Tail));
        Assert.Contains(_vectors.Budgets, static vector => vector.Mode == "render" && !vector.Valid && vector.Expression != ExpressionException.Echo(vector.Head + Repeat(vector.Link, vector.Count) + vector.Tail));
        Assert.All(_vectors.Budgets, static vector => Assert.True(vector.Valid ? vector.Work is > 0 && vector.Expression is null : vector.Work is null && vector.Expression is not null, vector.Name));

        // 带出的文本：不截断的、截断的、截断处是代理对的、三种求值方式
        Assert.Contains(_vectors.Echoes, static vector => vector.Arguments["expression"]!.GetValue<string>() == vector.Source);
        Assert.Contains(_vectors.Echoes, static vector => vector.Arguments["expression"]!.GetValue<string>().Length == _vectors.MaxEchoLength + 1);
        Assert.Contains(_vectors.Echoes, static vector => vector.Arguments["expression"]!.GetValue<string>() is { } echoed && echoed.Length == _vectors.MaxEchoLength && echoed != vector.Source);
        Assert.All<string>(["evaluate", "condition", "render"], static mode => Assert.Contains(_vectors.Echoes, vector => vector.Mode == mode));
    }

    [Fact]
    public void EveryExpressionMessageKey_HasACaseInTheVectors()
    {
        // 五个文件合起来，workflow.expression. 下的每个消息键至少有一条用例；向量里也不出现代码里没有的键
        var covered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in _files)
        {
            Collect(JsonNode.Parse(File.ReadAllText(SpecVectors.PathOf("workflow", file))), covered);
        }

        // 上限文件里的长度、嵌套、拼接与渲染用例不写消息键：不通过的那些各对应一个键
        if (_vectors.Lengths.Any(static vector => !vector.Valid))
        {
            covered.Add(WorkflowMessages.ExpressionTooLong);
        }

        if (_vectors.Nestings.Any(static vector => !vector.Valid))
        {
            covered.Add(WorkflowMessages.ExpressionTooDeep);
        }

        if (_vectors.Concatenations.Any(static vector => !vector.Valid) && _vectors.Renderings.Any(static vector => !vector.Valid))
        {
            covered.Add(WorkflowMessages.ExpressionStringTooLong);
        }

        if (_vectors.Budgets.Any(static vector => !vector.Valid))
        {
            covered.Add(WorkflowMessages.ExpressionBudgetExceeded);
        }

        var keys = ExpressionVectorReplay.MessageKeys();
        Assert.NotEmpty(keys);
        Assert.Equal(keys, covered);
    }

    /// <summary>
    /// 收集一个向量文件里出现的全部消息键
    /// </summary>
    private static void Collect(JsonNode? node, SortedSet<string> keys)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    if (name == "messageKey")
                    {
                        keys.Add(value!.GetValue<string>());
                    }
                    else
                    {
                        Collect(value, keys);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, keys);
                }

                break;
        }
    }

    /// <summary>
    /// 错误与期望的消息键、参数的出入
    /// </summary>
    /// <remarks>
    /// 一致返回 <see langword="null"/>；<paramref name="arguments"/> 不含 <c>expression</c>
    /// </remarks>
    private static string? Mismatch(Error? error, string messageKey, JsonObject arguments)
    {
        if (error is null)
        {
            return $"succeeds, the vector has '{messageKey}'.";
        }

        var actual = ExpressionVectorReplay.ArgumentsOf(error);
        actual.Remove("expression");
        return error.Code == _vectors.ErrorCode && error.MessageKey == messageKey && JsonNode.DeepEquals(actual, arguments)
            ? null
            : $"fails with {error.Code} '{error.MessageKey}' {actual.ToJsonString()}, the vector has '{messageKey}' {arguments.ToJsonString()}.";
    }

    /// <summary>
    /// 文件里的变量加上 <c>blocks</c> 给出的长字符串与 <c>arrays</c> 给出的长数组
    /// </summary>
    private static JsonObject BuildVariables()
    {
        var variables = (JsonObject)_vectors.Variables.DeepClone();
        foreach (var block in _vectors.Blocks)
        {
            variables[block.Variable] = Repeat(block.Unit, block.Repeat);
        }

        foreach (var array in _vectors.Arrays)
        {
            var items = new JsonArray();
            for (var index = 0; index < array.Repeat; index++)
            {
                items.Add(array.Item?.DeepClone());
            }

            variables[array.Variable] = items;
        }

        return variables;
    }

    /// <summary>
    /// 重复文本
    /// </summary>
    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="ErrorCode">失败用例的错误码</param>
    /// <param name="MaxLength">表达式的长度上限</param>
    /// <param name="MaxDepth">嵌套的层数上限</param>
    /// <param name="MaxStringLength">拼接与渲染出的文本的长度上限</param>
    /// <param name="MaxEchoLength">错误里带出的文本的长度上限</param>
    /// <param name="MaxWork">一次求值的工作量上限</param>
    /// <param name="StepWork">一步的工作量</param>
    /// <param name="Variables">实例变量</param>
    /// <param name="Blocks">很长的字符串变量</param>
    /// <param name="Arrays">很长的数组变量</param>
    /// <param name="Lengths">长度的用例</param>
    /// <param name="Nestings">嵌套的用例</param>
    /// <param name="Chains">链的用例</param>
    /// <param name="Concatenations">拼接的用例</param>
    /// <param name="Renderings">渲染的用例</param>
    /// <param name="Echoes">带出文本的用例</param>
    /// <param name="Budgets">工作量的用例</param>
    private sealed record LimitVectors(
        string ErrorCode,
        int MaxLength,
        int MaxDepth,
        int MaxStringLength,
        int MaxEchoLength,
        int MaxWork,
        int StepWork,
        JsonObject Variables,
        BlockCase[] Blocks,
        ArrayCase[] Arrays,
        LengthCase[] Lengths,
        NestingCase[] Nestings,
        ChainCase[] Chains,
        ConcatenationCase[] Concatenations,
        RenderingCase[] Renderings,
        EchoCase[] Echoes,
        BudgetCase[] Budgets);

    /// <summary>
    /// 一个很长的字符串变量
    /// </summary>
    /// <param name="Variable">变量名</param>
    /// <param name="Unit">重复的片段</param>
    /// <param name="Repeat">重复的次数</param>
    private sealed record BlockCase(string Variable, string Unit, int Repeat);

    /// <summary>
    /// 一个很长的数组变量
    /// </summary>
    /// <param name="Variable">变量名</param>
    /// <param name="Item">重复的元素</param>
    /// <param name="Repeat">元素的个数</param>
    private sealed record ArrayCase(string Variable, JsonNode? Item, int Repeat);

    /// <summary>
    /// 一条长度用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Head">开头</param>
    /// <param name="Padding">重复的片段</param>
    /// <param name="Count">重复的次数</param>
    /// <param name="Tail">结尾</param>
    /// <param name="Length">表达式的长度（UTF-16 码元）</param>
    /// <param name="Valid">是否在上限以内</param>
    private sealed record LengthCase(string Name, string Head, string Padding, int Count, string Tail, int Length, bool Valid);

    /// <summary>
    /// 一条嵌套用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Open">每一层的开头</param>
    /// <param name="Leaf">最里面的原子</param>
    /// <param name="Close">每一层的结尾</param>
    /// <param name="Levels">层数</param>
    /// <param name="Valid">是否在上限以内</param>
    private sealed record NestingCase(string Name, string Open, string Leaf, string Close, int Levels, bool Valid);

    /// <summary>
    /// 一条链的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Head">开头</param>
    /// <param name="Link">重复的一环</param>
    /// <param name="Count">环数</param>
    /// <param name="Type">结果的类型</param>
    /// <param name="Text">结果的文本</param>
    private sealed record ChainCase(string Name, string Head, string Link, int Count, string Type, string Text);

    /// <summary>
    /// 一条拼接上限的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Expression">表达式</param>
    /// <param name="Valid">是否在上限以内</param>
    /// <param name="Length">结果的长度，或错误的 <c>length</c> 参数</param>
    private sealed record ConcatenationCase(string Name, string Expression, bool Valid, long Length);

    /// <summary>
    /// 一条渲染上限的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Template">模板</param>
    /// <param name="Valid">是否在上限以内</param>
    /// <param name="Length">文本的长度，或错误的 <c>length</c> 参数</param>
    private sealed record RenderingCase(string Name, string Template, bool Valid, long Length);

    /// <summary>
    /// 一条带出文本的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Mode">求值的方式：<c>evaluate</c>、<c>condition</c> 或 <c>render</c></param>
    /// <param name="Source">表达式或模板</param>
    /// <param name="MessageKey">消息键</param>
    /// <param name="Arguments">全部参数，含 <c>expression</c></param>
    private sealed record EchoCase(string Name, string Mode, string Source, string MessageKey, JsonObject Arguments);

    /// <summary>
    /// 一条工作量的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Mode">求值的方式：<c>evaluate</c> 或 <c>render</c></param>
    /// <param name="Head">开头</param>
    /// <param name="Link">重复的片段</param>
    /// <param name="Count">重复的次数</param>
    /// <param name="Tail">结尾</param>
    /// <param name="Valid">是否在上限以内</param>
    /// <param name="Work">成功时累计的工作量；失败的用例没有</param>
    /// <param name="Expression">失败时错误带出的文本；成功的用例没有</param>
    private sealed record BudgetCase(string Name, string Mode, string Head, string Link, int Count, string Tail, bool Valid, long? Work = null, string? Expression = null);
}
