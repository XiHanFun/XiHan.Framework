// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 默认求值器。
/// </summary>
/// <remarks>
/// <see cref="ExpressionCompiler"/> 的小语言，编译结果按表达式文本缓存（超过上限整体清空）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowExpressionEvaluator : IWorkflowExpressionEvaluator
{
    /// <summary>
    /// 缓存上限。
    /// </summary>
    private readonly int _cacheSize;

    /// <summary>
    /// 表达式文本 → 编译结果。
    /// </summary>
    private readonly ConcurrentDictionary<string, CompiledExpression> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowExpressionEvaluator(WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _cacheSize = options.ExpressionCacheSize;
    }

    /// <inheritdoc />
    public Result Validate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        try
        {
            Get(expression);
            return Result.Success();
        }
        catch (ExpressionException exception)
        {
            return Invalid(expression, exception.Message);
        }
    }

    /// <inheritdoc />
    public Result<Maybe<JsonNode>> Evaluate(string expression, ExpressionScope scope)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(scope);
        try
        {
            // 原语的成功结果不携带 null：null 结果是成功的无值
            return Result.Success(Maybe.From(Get(expression)(scope)));
        }
        catch (ExpressionException exception)
        {
            return Invalid(expression, exception.Message);
        }
    }

    /// <inheritdoc />
    public Result<bool> EvaluateCondition(string expression, ExpressionScope scope)
    {
        var evaluated = Evaluate(expression, scope);
        if (!evaluated.TryGetValue(out var value))
        {
            return evaluated.Error!;
        }

        if (value.OrNull() is JsonValue scalar && !scalar.TryGetValue<string>(out _) && WorkflowValues.TryBool(scalar, out var flag))
        {
            return flag;
        }

        return Invalid(expression, "The result is not a boolean.");
    }

    /// <inheritdoc />
    public Result<string> Render(string source, ExpressionScope scope)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scope);
        var builder = new StringBuilder(source.Length);
        var position = 0;
        while (position < source.Length)
        {
            var open = source.IndexOf("{{", position, StringComparison.Ordinal);
            if (open < 0)
            {
                builder.Append(source, position, source.Length - position);
                break;
            }

            builder.Append(source, position, open - position);
            var close = FindClose(source, open + 2);
            if (close < 0)
            {
                return Invalid(source, "Unterminated '{{' placeholder.");
            }

            var expression = source[(open + 2)..close].Trim();
            var evaluated = Evaluate(expression, scope);
            if (!evaluated.TryGetValue(out var value))
            {
                return evaluated.Error!;
            }

            builder.Append(value.OrNull() switch
            {
                null => string.Empty,
                JsonValue scalar => WorkflowValues.AsString(scalar),
                var node => node.ToJsonString(),
            });
            position = close + 2;
        }

        return builder.ToString();
    }

    /// <summary>
    /// 找占位的 <c>}}</c>，跳过引号里的。
    /// </summary>
    private static int FindClose(string template, int start)
    {
        char? quote = null;
        for (var i = start; i < template.Length; i++)
        {
            var c = template[i];
            if (quote is not null)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 取或编译。
    /// </summary>
    /// <remarks>
    /// 缓存超过上限整体清空（表达式集合通常很小，清空比淘汰便宜）。
    /// </remarks>
    private CompiledExpression Get(string expression)
    {
        if (_cache.TryGetValue(expression, out var compiled))
        {
            return compiled;
        }

        compiled = ExpressionCompiler.Compile(expression);
        if (_cache.Count >= _cacheSize)
        {
            _cache.Clear();
        }

        _cache[expression] = compiled;
        return compiled;
    }

    /// <summary>
    /// 表达式错误。
    /// </summary>
    private static Error Invalid(string expression, string reason)
        => Error.Validation(WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionInvalid, MessageArguments.Of(("expression", expression), ("reason", reason)));
}
