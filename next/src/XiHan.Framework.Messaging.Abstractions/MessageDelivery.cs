// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 一次投递：主题、信封与第几次尝试（从 1 起）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageDelivery(string Topic, MessageEnvelope Envelope, int Attempt)
{
    /// <summary>
    /// 主题。
    /// </summary>
    public string Topic { get; } = TopicName.Validate(Topic);

    /// <summary>
    /// 信封。
    /// </summary>
    public MessageEnvelope Envelope { get; } = Envelope ?? throw new ArgumentNullException(nameof(Envelope));

    /// <summary>
    /// 第几次尝试。
    /// </summary>
    public int Attempt { get; } = Attempt >= 1 ? Attempt : throw new ArgumentOutOfRangeException(nameof(Attempt), Attempt, "尝试次数从 1 起。");
}
