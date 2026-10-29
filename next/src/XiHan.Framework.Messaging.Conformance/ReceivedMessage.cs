// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 套件记录的一次到达：投递、Baggage 快照、追踪 ID 与调用来源。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record ReceivedMessage(MessageDelivery Delivery, IReadOnlyDictionary<string, string> Baggage, string TraceId)
{
    /// <summary>
    /// 派发时调用上下文里的来源（RFC-0003）。
    /// </summary>
    /// <remarks>
    /// 没有时为 <see langword="null"/>。
    /// </remarks>
    public InvocationOrigin? Origin { get; init; }
}
