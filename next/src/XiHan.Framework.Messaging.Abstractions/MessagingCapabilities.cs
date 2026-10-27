// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingCapabilities
{
    /// <summary>
    /// 工作队列：至少一次投递、重试与死信。
    /// </summary>
    public const string Queue = "messaging.queue";

    /// <summary>
    /// 流（预留）。
    /// </summary>
    public const string Stream = "messaging.stream";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
