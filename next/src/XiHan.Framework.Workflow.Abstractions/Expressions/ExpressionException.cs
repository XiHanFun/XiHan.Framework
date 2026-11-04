// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 表达式的语法或求值错误
/// </summary>
/// <remarks>
/// 带消息键与参数（RFC-0010，ADR-0106），在求值器边界经 <see cref="ToError"/> 翻译成 <see cref="WorkflowErrors.ExpressionInvalid"/>；异常消息只是键与参数的描述，不给人看
/// </remarks>
internal sealed class ExpressionException : Exception
{
    /// <summary>
    /// 错误里带上的表达式文本长度上限
    /// </summary>
    private const int MaxEchoLength = 200;

    /// <summary>
    /// 创建
    /// </summary>
    public ExpressionException(string messageKey, IReadOnlyDictionary<string, object?>? arguments = null)
        : base(MessageTemplate.Describe(messageKey, arguments))
    {
        MessageKey = messageKey;
        Arguments = arguments;
    }

    /// <summary>
    /// 消息键（<c>workflow.expression.*</c>）
    /// </summary>
    public string MessageKey { get; }

    /// <summary>
    /// 消息参数（不含表达式本身）
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Arguments { get; }

    /// <summary>
    /// 转成错误：表达式文本放在参数 <c>expression</c>，其余参数随后
    /// </summary>
    /// <remarks>
    /// 错误会进校验结果、响应和实例故障，表达式文本最多带 <see cref="MaxEchoLength"/> 个字符
    /// </remarks>
    public Error ToError(string expression)
    {
        var echoed = expression.Length > MaxEchoLength ? string.Concat(expression.AsSpan(0, MaxEchoLength), "…") : expression;
        var details = new Dictionary<string, object?>(StringComparer.Ordinal) { ["expression"] = echoed };
        foreach (var (name, value) in Arguments ?? new Dictionary<string, object?>())
        {
            details[name] = value;
        }

        return Error.Validation(WorkflowErrors.ExpressionInvalid, MessageKey, details);
    }
}
