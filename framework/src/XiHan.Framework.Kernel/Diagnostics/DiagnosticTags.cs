// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 内核的 Span 与指标上的标签名
/// </summary>
/// <remarks>
/// 名字是对外可观察的契约（RFC-0023、<c>spec/plugin/invocation.md</c> §6，ADR-0269）：改名就是让已有的看板与告警失效。插件宿主的 Span 用同一组名字
/// </remarks>
internal static class DiagnosticTags
{
    /// <summary>
    /// 调用的结果：<c>success</c>、<c>failure</c> 或 <c>unset</c>
    /// </summary>
    public const string Outcome = "xihan.outcome";

    /// <summary>
    /// 被调用的操作 ID
    /// </summary>
    /// <remarks>
    /// 只取上下文里操作描述的 ID（调用器与宿主在管道开始前放进去，来自登记表、数量有上限）。不是操作调用时不带这个标签：消息处理器、作业、直接交给 <c>App.InvokeAsync</c> 的处理器与没有匹配到操作端点的请求都没有操作描述，主题、作业名、请求路径这类取值不受登记表约束的文本不进标签
    /// </remarks>
    public const string Operation = "xihan.operation";
}
