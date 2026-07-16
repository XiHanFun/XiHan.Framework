#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowExpressionEvaluator
// Guid:7c2f95e1-0b46-4d83-a917-56e08d24cf39
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:47:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Expressions;

/// <summary>
/// 内置工作流表达式求值器
/// </summary>
/// <remarks>
/// 轻量表达式语言：变量引用（点号导航/索引）、数字与字符串字面量、true/false/null、
/// 算术（+ - * / %）、比较（== != &lt; &lt;= &gt; &gt;=）、逻辑（&amp;&amp; || !，短路）、
/// 内置函数（len/contains/startsWith/endsWith/upper/lower/trim/isNullOrEmpty/abs/min/max/round/toNumber/toString/now/date）。
/// 数字统一以 decimal 求值；语法树按表达式文本缓存复用。
/// </remarks>
public class WorkflowExpressionEvaluator : IWorkflowExpressionEvaluator
{
    private const int MaxCacheSize = 10000;

    private readonly ConcurrentDictionary<string, ExpressionNode> _cache = new();
    private readonly IClock _clock;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clock">时钟</param>
    public WorkflowExpressionEvaluator(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EvaluateCore(expression, variables));
    }

    /// <inheritdoc />
    public Task<T?> EvaluateAsync<T>(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var result = EvaluateCore(expression, variables);
        return Task.FromResult(WorkflowValueConverter.ConvertTo<T>(result));
    }

    /// <inheritdoc />
    public Task<bool> EvaluateConditionAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var result = EvaluateCore(expression, variables);
        return result is bool boolean
            ? Task.FromResult(boolean)
            : throw new WorkflowException($"条件表达式必须返回布尔值：{expression}");
    }

    /// <inheritdoc />
    public Task<string> RenderTemplateAsync(
        string template,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return Task.FromResult(template);
        }

        var builder = new StringBuilder(template.Length);
        var index = 0;

        while (index < template.Length)
        {
            var start = template.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                builder.Append(template, index, template.Length - index);
                break;
            }

            builder.Append(template, index, start - index);

            var end = FindPlaceholderEnd(template, start + 2);
            if (end < 0)
            {
                throw new WorkflowException($"模板存在未闭合的表达式占位：{template}");
            }

            var expression = template[(start + 2)..end].Trim();
            if (expression.Length == 0)
            {
                throw new WorkflowException($"模板存在空表达式占位：{template}");
            }

            var value = EvaluateCore(expression, variables);
            builder.Append(ExpressionOperations.ToDisplayString(value));
            index = end + 2;
        }

        return Task.FromResult(builder.ToString());
    }

    /// <summary>
    /// 校验表达式语法（供定义校验器使用，非法时抛出工作流异常）
    /// </summary>
    /// <param name="expression">表达式文本</param>
    public static void ValidateSyntax(string expression)
    {
        _ = ExpressionParser.Parse(expression);
    }

    /// <summary>
    /// 查找占位结束位置（跳过表达式字符串字面量内的 <c>}}</c>）
    /// </summary>
    /// <param name="template">模板文本</param>
    /// <param name="searchFrom">占位内容起始位置</param>
    /// <returns>结束标记位置（未找到返回 -1）</returns>
    private static int FindPlaceholderEnd(string template, int searchFrom)
    {
        char? quote = null;

        for (var position = searchFrom; position < template.Length; position++)
        {
            var current = template[position];

            if (quote is { } activeQuote)
            {
                if (current == '\\' && position + 1 < template.Length)
                {
                    position++;
                    continue;
                }

                if (current == activeQuote)
                {
                    quote = null;
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == '}' && position + 1 < template.Length && template[position + 1] == '}')
            {
                return position;
            }
        }

        return -1;
    }

    private object? EvaluateCore(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new WorkflowException("表达式不能为空");
        }

        // 缓存超限时整体重置，表达式来源于有限的流程定义，实践中不会频繁触发
        if (_cache.Count > MaxCacheSize)
        {
            _cache.Clear();
        }

        var node = _cache.GetOrAdd(expression, static text => ExpressionParser.Parse(text));

        try
        {
            return node.Evaluate(new ExpressionEvaluationContext(variables, () => _clock.Now));
        }
        catch (WorkflowException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WorkflowException($"表达式求值失败：{expression}", ex);
        }
    }
}
