// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 默认求值器
/// </summary>
/// <remarks>
/// <see cref="ExpressionCompiler"/> 的小语言，文法与语义照 RFC-0021「表达式」，编译结果按表达式文本缓存（超过上限整体清空）。每次 <see cref="Evaluate(string, ExpressionScope)"/> 与 <see cref="EvaluateCondition"/> 各有一份工作量，一次 <see cref="Render(string, ExpressionScope)"/> 的全部占位共用一份（<see cref="ExpressionRun"/>，ADR-0296）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowExpressionEvaluator : IWorkflowExpressionEvaluator
{
    /// <summary>
    /// 缓存上限
    /// </summary>
    private readonly int _cacheSize;

    /// <summary>
    /// 表达式文本 → 编译结果
    /// </summary>
    private readonly ConcurrentDictionary<string, CompiledExpression> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建
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
            return exception.ToError(expression);
        }
    }

    /// <inheritdoc />
    public Result<Maybe<JsonNode>> Evaluate(string expression, ExpressionScope scope) => Evaluate(expression, scope, out _);

    /// <summary>
    /// 求值，并交出这次求值累计的工作量
    /// </summary>
    /// <remarks>
    /// 工作量按 RFC-0021「表达式」的「长度与上限」计（ADR-0296）；失败时是失败之前记下的，超过上限时含使它超过的那一笔
    /// </remarks>
    /// <param name="expression">表达式</param>
    /// <param name="scope">作用域</param>
    /// <param name="work">累计的工作量</param>
    internal Result<Maybe<JsonNode>> Evaluate(string expression, ExpressionScope scope, out long work)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(scope);
        var run = new ExpressionRun(scope);
        var evaluated = Evaluate(expression, run);
        work = run.Work;
        return evaluated;
    }

    /// <inheritdoc />
    public Result<bool> EvaluateCondition(string expression, ExpressionScope scope)
    {
        var evaluated = Evaluate(expression, scope);
        if (!evaluated.TryGetValue(out var value))
        {
            return evaluated.Error!;
        }

        // 只认 JSON 的布尔值：字符串 "true"、数字与无值都不是
        if (value.OrNull() is JsonValue scalar && scalar.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            return scalar.GetValueKind() == JsonValueKind.True;
        }

        return Error.Validation(WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionNotBoolean, MessageArguments.Of(("expression", ExpressionException.Echo(expression))));
    }

    /// <inheritdoc />
    public Result<string> Render(string source, ExpressionScope scope) => Render(source, scope, out _);

    /// <summary>
    /// 渲染模板，并交出全部占位累计的工作量
    /// </summary>
    /// <remarks>
    /// 全部占位共用一份工作量：占位里的表达式照常记，结果在接进文本之前再记它的读取量（ADR-0296）
    /// </remarks>
    /// <param name="source">模板</param>
    /// <param name="scope">作用域</param>
    /// <param name="work">累计的工作量</param>
    internal Result<string> Render(string source, ExpressionScope scope, out long work)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scope);
        var run = new ExpressionRun(scope);
        var rendered = Render(source, run);
        work = run.Work;
        return rendered;
    }

    /// <summary>
    /// 在一次求值的状态上求值
    /// </summary>
    /// <remarks>
    /// 语法与求值的错误都带这段表达式
    /// </remarks>
    private Result<Maybe<JsonNode>> Evaluate(string expression, ExpressionRun run)
    {
        try
        {
            // 原语的成功结果不携带 null：null 结果是成功的无值
            return Result.Success(Maybe.From(Get(expression)(run)));
        }
        catch (ExpressionException exception)
        {
            return exception.ToError(expression);
        }
    }

    /// <summary>
    /// 在一次渲染的状态上渲染
    /// </summary>
    private Result<string> Render(string source, ExpressionRun run)
    {
        var builder = new StringBuilder(source.Length);
        var position = 0;
        while (position < source.Length)
        {
            var open = source.IndexOf("{{", position, StringComparison.Ordinal);
            var literal = (open < 0 ? source.Length : open) - position;
            if (TooLong(source, builder.Length, literal) is { } literalTooLong)
            {
                return literalTooLong;
            }

            builder.Append(source, position, literal);
            if (open < 0)
            {
                break;
            }

            var close = FindClose(source, open + 2);
            if (close < 0)
            {
                return Error.Validation(WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionUnterminatedPlaceholder, MessageArguments.Of(("expression", ExpressionException.Echo(source))));
            }

            var expression = source[(open + 2)..close].Trim();
            var evaluated = Evaluate(expression, run);
            if (!evaluated.TryGetValue(out var value))
            {
                return evaluated.Error!;
            }

            // 写成文本之前记它的读取量：占位可以是整个变量，几十万个值的容器写成文本要遍历一遍；在这里超过的，错误带的是模板，与长度超过上限相同
            try
            {
                run.Read(value.OrNull());
            }
            catch (ExpressionException exception)
            {
                return exception.ToError(source);
            }

            var text = WorkflowValueText.Of(value.OrNull());
            if (TooLong(source, builder.Length, text.Length) is { } placeholderTooLong)
            {
                return placeholderTooLong;
            }

            builder.Append(text);
            position = close + 2;
        }

        return builder.ToString();
    }

    /// <summary>
    /// 渲染结果再接上一段会不会超过长度上限
    /// </summary>
    /// <remarks>
    /// 占位的结果可以是整个变量的 JSON 文本，一个模板里重复几百次就能把内存耗尽：字面文本与每个占位的文本接上去之前都按累计长度核对，上限与 <c>+</c> 拼接相同（ADR-0287）。超过时返回错误，<c>length</c> 是接上这一段之后的长度，<c>expression</c> 是截断的模板
    /// </remarks>
    private static Error? TooLong(string source, int rendered, int next)
    {
        var length = (long)rendered + next;
        return length > ExpressionCompiler.MaxStringLength
            ? Error.Validation(WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionStringTooLong, MessageArguments.Of(("expression", ExpressionException.Echo(source)), ("length", length), ("max", ExpressionCompiler.MaxStringLength)))
            : null;
    }

    /// <summary>
    /// 找占位的 <c>}}</c>，跳过引号里的
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
    /// 取或编译
    /// </summary>
    /// <remarks>
    /// 缓存超过上限整体清空（表达式集合通常很小，清空比淘汰便宜）
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
}
