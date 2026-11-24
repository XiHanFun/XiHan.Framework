// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放表达式向量时共用的求值器、作用域与比较
/// </summary>
/// <remarks>
/// 规范 RFC-0021「表达式」（ADR-0287）的五个向量文件各有一个测试类回放；这里是它们共用的部分。向量是规范的一部分，只回放，不另存一份
/// </remarks>
internal static class ExpressionVectorReplay
{
    /// <summary>
    /// 向量没有给时刻时用的时刻
    /// </summary>
    private const string DefaultNow = "2026-10-26T08:00:00.1234567+00:00";

    /// <summary>
    /// 默认选项的求值器
    /// </summary>
    public static WorkflowExpressionEvaluator Evaluator { get; } = new(new WorkflowOptions());

    /// <summary>
    /// 以向量的变量与时刻建作用域
    /// </summary>
    /// <remarks>
    /// 变量每次深拷贝：一条用例改不到下一条用例的变量
    /// </remarks>
    public static ExpressionScope Scope(JsonObject variables, string? now = null)
        => new((JsonObject)variables.DeepClone(), new FakeTimeProvider(DateTimeOffset.Parse(now ?? DefaultNow, CultureInfo.InvariantCulture)));

    /// <summary>
    /// 结果的类型名：JSON 的六种值
    /// </summary>
    public static string TypeOf(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "array",
        JsonObject => "object",
        _ => node.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => "number",
            JsonValueKind.String => "string",
            var kind => kind.ToString(),
        },
    };

    /// <summary>
    /// 回放求值成功的用例
    /// </summary>
    /// <remarks>
    /// 语法合法；结果的类型与文本同向量；只有一个占位的模板渲染出来的也是这段文本
    /// </remarks>
    public static void ReplayValues(ExpressionCases vectors, List<string> failures)
    {
        foreach (var vector in vectors.Values)
        {
            var scope = Scope(vectors.Variables, vectors.Now);
            if (Evaluator.Validate(vector.Expression).Error is { } invalid)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' is rejected with '{invalid.MessageKey}'.");
                continue;
            }

            var evaluated = Evaluator.Evaluate(vector.Expression, scope);
            if (evaluated.Error is { } error)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' fails with '{error.MessageKey}', the vector has {vector.Type} '{vector.Text}'.");
                continue;
            }

            var node = evaluated.ValueOrThrow().OrNull();
            var type = TypeOf(node);
            var text = WorkflowValueText.Of(node);
            if (type != vector.Type || !string.Equals(text, vector.Text, StringComparison.Ordinal))
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' is {type} '{text}', the vector has {vector.Type} '{vector.Text}'.");
            }

            var rendered = Evaluator.Render("{{" + vector.Expression + "}}", Scope(vectors.Variables, vectors.Now));
            if (rendered.IsFailure || !string.Equals(rendered.ValueOrThrow(), vector.Text, StringComparison.Ordinal))
            {
                failures.Add($"{vector.Name}: the template of '{vector.Expression}' does not render '{vector.Text}'.");
            }
        }
    }

    /// <summary>
    /// 回放求值失败的用例
    /// </summary>
    /// <remarks>
    /// 错误码、消息键与参数同向量，<c>expression</c> 参数是表达式原文；<c>syntax</c> 为真时只校验语法就报同一个错误，为假时语法合法
    /// </remarks>
    public static void ReplayErrors(ExpressionCases vectors, List<string> failures)
    {
        foreach (var vector in vectors.Errors)
        {
            var evaluated = Evaluator.Evaluate(vector.Expression, Scope(vectors.Variables, vectors.Now));
            if (Mismatch(evaluated.Error, vectors.ErrorCode, vector.MessageKey, vector.Arguments, vector.Expression) is { } mismatch)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' {mismatch}");
            }

            var validated = Evaluator.Validate(vector.Expression);
            if (vector.Syntax)
            {
                if (Mismatch(validated.Error, vectors.ErrorCode, vector.MessageKey, vector.Arguments, vector.Expression) is { } syntax)
                {
                    failures.Add($"{vector.Name}: validating '{vector.Expression}' {syntax}");
                }
            }
            else if (validated.Error is { } unexpected)
            {
                failures.Add($"{vector.Name}: '{vector.Expression}' must be valid syntax, validation fails with '{unexpected.MessageKey}'.");
            }
        }
    }

    /// <summary>
    /// 错误与向量的出入
    /// </summary>
    /// <remarks>
    /// 一致返回 <see langword="null"/>。<paramref name="arguments"/> 不含 <c>expression</c>，它单独与 <paramref name="expression"/> 核对
    /// </remarks>
    public static string? Mismatch(Error? error, string errorCode, string messageKey, JsonObject arguments, string expression)
    {
        if (error is null)
        {
            return $"succeeds, the vector has '{messageKey}'.";
        }

        var actual = ArgumentsOf(error);
        var echoed = actual["expression"]?.GetValue<string>();
        actual.Remove("expression");
        if (error.Code != errorCode || error.MessageKey != messageKey || !JsonNode.DeepEquals(actual, arguments) || !string.Equals(echoed, expression, StringComparison.Ordinal))
        {
            return $"fails with {error.Code} '{error.MessageKey}' {actual.ToJsonString()} (expression '{echoed}'), the vector has {errorCode} '{messageKey}' {arguments.ToJsonString()} (expression '{expression}').";
        }

        return null;
    }

    /// <summary>
    /// 错误的全部参数，写成 JSON 对象
    /// </summary>
    public static JsonObject ArgumentsOf(Error error)
    {
        var arguments = new JsonObject();
        foreach (var (name, value) in error.Details ?? new Dictionary<string, object?>())
        {
            arguments[name] = value switch
            {
                null => null,
                string text => JsonValue.Create(text),
                int number => JsonValue.Create(number),
                long number => JsonValue.Create(number),
                decimal number => JsonValue.Create(number),
                _ => throw new InvalidOperationException($"Argument '{name}' has the unexpected type '{value.GetType()}'."),
            };
        }

        return arguments;
    }

    /// <summary>
    /// <see cref="WorkflowMessages"/> 里表达式的全部消息键
    /// </summary>
    public static IReadOnlyList<string> MessageKeys()
        => [.. typeof(WorkflowMessages).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral)
            .Select(static field => (string)field.GetRawConstantValue()!)
            .Where(static key => key.StartsWith("workflow.expression.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// 求值成功与失败两张表的向量文件
    /// </summary>
    /// <param name="ErrorCode">失败用例的错误码</param>
    /// <param name="Variables">求值时的实例变量</param>
    /// <param name="Values">求值成功的用例</param>
    /// <param name="Errors">求值失败的用例</param>
    /// <param name="Now">求值时的时刻；文件没有给时为 <see langword="null"/></param>
    internal sealed record ExpressionCases(string ErrorCode, JsonObject Variables, ValueCase[] Values, ErrorCase[] Errors, string? Now = null);

    /// <summary>
    /// 一条求值成功的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Expression">表达式</param>
    /// <param name="Type">结果的类型</param>
    /// <param name="Text">结果的文本</param>
    internal sealed record ValueCase(string Name, string Expression, string Type, string Text);

    /// <summary>
    /// 一条求值失败的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Expression">表达式</param>
    /// <param name="Syntax">是否解析时就报出</param>
    /// <param name="MessageKey">消息键</param>
    /// <param name="Arguments">除 <c>expression</c> 之外的参数</param>
    internal sealed record ErrorCase(string Name, string Expression, bool Syntax, string MessageKey, JsonObject Arguments);
}
