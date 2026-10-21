// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息运行时：启动时让代理消费全部订阅主题并把投递交给 <see cref="MessageDispatcher"/>，停止时等待进行中的投递结束。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessagingRuntime(IMessageBroker broker, MessageDispatcher dispatcher, MessageSubscriptions subscriptions)
{
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    private readonly MessageDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    private readonly MessageSubscriptions _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));

    /// <summary>
    /// 是否已启动。
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// 启动消费。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            return;
        }

        await _broker.StartAsync(_subscriptions.Topics.ToList(), _dispatcher.DispatchAsync, cancellationToken).ConfigureAwait(false);
        IsRunning = true;
    }

    /// <summary>
    /// 停止消费。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        await _broker.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
