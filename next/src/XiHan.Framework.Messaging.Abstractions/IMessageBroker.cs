// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息传输，Provider 实现它：发布、按主题消费、至少一次投递、按 <see cref="MessagingOptions.MaxAttempts"/> 重试后死信。
/// 基础设施失败抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的 <see cref="FrameworkException"/>；队列已满返回 <see cref="MessagingErrors.QueueFull"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageBroker
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// 开始消费这些主题，每条消息调用 <paramref name="dispatch"/>。
    /// </summary>
    Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken);

    /// <summary>
    /// 停止消费并等待进行中的投递结束。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
