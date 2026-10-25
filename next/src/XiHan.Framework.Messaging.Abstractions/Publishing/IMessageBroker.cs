// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 消息传输，Provider 实现它：发布、按主题消费、至少一次投递、按 <see cref="MessagingOptions.MaxAttempts"/> 重试后死信。
/// 基础设施失败抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的 <see cref="FrameworkException"/>；队列已满返回 <see cref="MessagingErrors.QueueFull"/>。
/// 主题由发布者校验，代理收到不合法主题是装配错误。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageBroker
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// 批量发布到同一主题，用代理的原生批量一次完成；结果与入参顺序一一对应，主题内保持入参顺序，空集合不触代理。
    /// 基础设施失败整批抛出；容量不足时整批返回 <see cref="MessagingErrors.QueueFull"/>，不写入半批。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken);

    /// <summary>
    /// 开始消费这些主题，每条消息调用 <paramref name="dispatch"/>。
    /// </summary>
    Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken);

    /// <summary>
    /// 停止消费并等待进行中的投递结束。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
