// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 按脚本给出逐项结果的代理。
/// </summary>
/// <remarks>
/// 记录每次发布的事件 ID；<see cref="Outcome"/> 决定每一项的结果，<see cref="Unreachable"/> 时整批抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的异常。不消费。
/// </remarks>
internal sealed class ScriptedBroker : IMessageBroker
{
    /// <summary>
    /// 发布过的事件 ID，按发布顺序。
    /// </summary>
    public List<string> Published { get; } = [];

    /// <summary>
    /// 每一项的结果。
    /// </summary>
    /// <remarks>
    /// 缺省全部成功。
    /// </remarks>
    public Func<MessageEnvelope, Result> Outcome { get; set; } = static _ => Result.Success();

    /// <summary>
    /// 是否模拟代理不可达。
    /// </summary>
    public bool Unreachable { get; set; }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        => (await PublishManyAsync(topic, [envelope], cancellationToken).ConfigureAwait(false))[0];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        if (Unreachable)
        {
            throw new FrameworkException(MessagingErrors.BrokerUnavailable, "The scripted broker is unreachable.");
        }

        Published.AddRange(envelopes.Select(static e => e.Id));
        return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(Outcome)]);
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
