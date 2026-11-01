// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 握手声明的一个事件订阅。
/// </summary>
/// <param name="Topic">主题。</param>
/// <param name="MessageType">只投递该 CloudEvents 类型；空为全部。</param>
/// <param name="Mode">订阅方式：竞争或广播（RFC-0009「订阅方式」）。</param>
internal sealed record PluginSubscription(string Topic, string? MessageType, SubscriptionMode Mode)
{
    /// <summary>
    /// 该投递是否命中本订阅。
    /// </summary>
    /// <remarks>
    /// 主题、订阅方式都要相同，类型为空时不限类型：以竞争方式订阅的插件收不到广播投递，反之亦然。
    /// </remarks>
    public bool Matches(string topic, string messageType, SubscriptionMode mode)
        => Mode == mode && string.Equals(Topic, topic, StringComparison.Ordinal) && (MessageType is null || string.Equals(MessageType, messageType, StringComparison.Ordinal));
}
