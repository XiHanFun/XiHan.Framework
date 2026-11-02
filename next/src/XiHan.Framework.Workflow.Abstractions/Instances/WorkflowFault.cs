// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 落在存储里的故障：错误码、消息键与参数。
/// </summary>
/// <remarks>
/// 面向人的文案不存进库（RFC-0010，ADR-0105）：展示用 <see cref="Localize"/>，按当前消息作用域（调用链里是调用方的区域）渲染；返回给调用方用 <see cref="ToError"/>，由宿主在 Egress 翻译。<see cref="Message"/> 是写入时按进程默认区域渲染的文本，供日志用；没有消息键的故障（自由文本，或升级前写入的）只有它。
/// </remarks>
/// <param name="Code">错误码。</param>
/// <param name="Message">按进程默认区域渲染的消息（日志用）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowFault(string Code, string Message)
{
    /// <summary>
    /// 消息键。
    /// </summary>
    /// <remarks>
    /// 自由文本的故障与升级前写入的故障为 <see langword="null"/>。
    /// </remarks>
    public string? MessageKey { get; init; }

    /// <summary>
    /// 消息参数，值已按不变文化转成字符串。
    /// </summary>
    /// <remarks>
    /// 应用或第三方给的文本（异常消息、HTTP 原因短语、外部任务工作者报的错误、<c>Fault</c> 节点作者写的消息）原样在这里，外层的消息键照常本地化。
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Arguments { get; init; }

    /// <summary>
    /// 内层原因。
    /// </summary>
    /// <remarks>
    /// 子流程的故障、存储不可达的根因等；没有为 <see langword="null"/>。
    /// </remarks>
    public WorkflowFault? Inner { get; init; }

    /// <summary>
    /// 从错误创建。
    /// </summary>
    /// <remarks>
    /// 带消息键的错误存键与参数（按不变文化转成字符串），连同内层错误一起存；自由文本的错误只存文本。
    /// </remarks>
    public static WorkflowFault From(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkflowFault(error.Code, error.Message)
        {
            MessageKey = error.MessageKey,
            Arguments = error.Details?.ToDictionary(static pair => pair.Key, static pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.Ordinal),
            Inner = error.Inner is null ? null : From(error.Inner),
        };
    }

    /// <summary>
    /// 按当前消息作用域的区域渲染的文案。
    /// </summary>
    /// <remarks>
    /// 调用链里是调用方的区域（<see cref="MessageScope.Current"/>，RFC-0010），调用之外是进程默认区域。有内层原因时以 <c>workflow.fault.caused_by</c> 把内层的文案接在后面；没有消息键时是 <see cref="Message"/> 原文。
    /// </remarks>
    public string Localize()
    {
        var text = MessageKey is { } key ? key.L(Details()) : Message;
        // 外层说发生了什么，内层说为什么（子流程为何失败、存储为何不可达），两段都按同一个区域渲染
        return Inner is null ? text : WorkflowMessages.FaultCausedBy.L(("message", text), ("cause", Inner.Localize()));
    }

    /// <summary>
    /// 转回错误。
    /// </summary>
    /// <remarks>
    /// 有消息键时是带键、参数与内层的错误，宿主在 Egress 按调用方的区域翻译；没有消息键时是自由文本的错误（线上还原，文本原样）。
    /// </remarks>
    public Error ToError()
        => MessageKey is { } key
            ? new Error(Code, key, Details(), Inner?.ToError())
            : Error.Literal(Code, Message, Details(), Inner?.ToError());

    /// <summary>
    /// 参数转成消息参数。
    /// </summary>
    private Dictionary<string, object?>? Details()
        => Arguments?.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value, StringComparer.Ordinal);
}
