// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using RabbitMQ.Client;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 打桩通道记下的一次发布。
/// </summary>
/// <param name="RoutingKey">路由键（默认交换机下即队列名）。</param>
/// <param name="Mandatory">是否 mandatory。</param>
/// <param name="Properties">消息属性。</param>
/// <param name="Body">消息体副本。</param>
public sealed record PublishedMessage(string RoutingKey, bool Mandatory, IReadOnlyBasicProperties Properties, byte[] Body)
{
    /// <summary>
    /// 尝试次数头。
    /// </summary>
    /// <remarks>
    /// 没有时为 0。
    /// </remarks>
    public int Attempt => Properties.Headers?.TryGetValue("x-xihan-attempt", out var raw) == true && raw is int attempt ? attempt : 0;
}
