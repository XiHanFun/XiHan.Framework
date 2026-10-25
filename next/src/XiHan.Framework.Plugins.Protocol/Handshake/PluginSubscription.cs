// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 握手声明的一个事件订阅。
/// </summary>
/// <param name="Topic">主题。</param>
/// <param name="MessageType">只投递该 CloudEvents 类型；空为全部。</param>
internal sealed record PluginSubscription(string Topic, string? MessageType)
{
    /// <summary>
    /// 该投递是否命中本订阅。
    /// </summary>
    public bool Matches(string topic, string messageType)
        => string.Equals(Topic, topic, StringComparison.Ordinal) && (MessageType is null || string.Equals(MessageType, messageType, StringComparison.Ordinal));
}
