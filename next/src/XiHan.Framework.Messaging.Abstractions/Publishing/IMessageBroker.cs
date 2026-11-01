// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 消息传输，Provider 实现它。
/// </summary>
/// <remarks>
/// 发布、按订阅（主题加方式）消费、至少一次投递、按 <see cref="MessagingOptions.MaxAttempts"/> 重试后死信（广播订阅到上限后丢弃）。基础设施失败抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的 <see cref="FrameworkException"/>；队列已满返回 <see cref="MessagingErrors.QueueFull"/>。主题由发布者校验，代理收到不合法主题是装配错误。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageBroker
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// 批量发布到同一主题，用代理的原生批量一次完成。
    /// </summary>
    /// <remarks>
    /// 结果与入参顺序一一对应，主题内保持入参顺序，空集合不触代理。基础设施失败整批抛出；容量不足时整批返回 <see cref="MessagingErrors.QueueFull"/>，不写入半批。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken);

    /// <summary>
    /// 开始消费这些订阅，每条消息调用 <paramref name="dispatch"/>。
    /// </summary>
    /// <remarks>
    /// 每个订阅（主题加方式）一份消费：竞争订阅在订阅了主题的实例之间分摊，广播订阅每个实例各一份（RFC-0009「订阅方式」）；投递的 <see cref="MessageDelivery.Mode"/> 是它来自的订阅的方式。
    /// </remarks>
    Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken);

    /// <summary>
    /// 停止消费并等待进行中的投递结束。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
