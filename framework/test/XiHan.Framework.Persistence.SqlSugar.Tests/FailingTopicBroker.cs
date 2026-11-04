// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 事件 ID 以 <c>fail</c> 开头的一律失败、其余成功的代理
/// </summary>
/// <remarks>
/// 失败原因带一段很长的文本，验证 <c>LastError</c> 是长文本列。不消费
/// </remarks>
internal sealed class FailingTopicBroker : IMessageBroker
{
    /// <summary>
    /// 发布过的事件 ID，按发布顺序
    /// </summary>
    public List<string> Published { get; } = [];

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        => (await PublishManyAsync(topic, [envelope], cancellationToken).ConfigureAwait(false))[0];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        Published.AddRange(envelopes.Select(static e => e.Id));
        return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(static e => e.Id.StartsWith("fail", StringComparison.Ordinal)
            ? Result.Failure(Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", new string('x', 600)))))
            : Result.Success())]);
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
