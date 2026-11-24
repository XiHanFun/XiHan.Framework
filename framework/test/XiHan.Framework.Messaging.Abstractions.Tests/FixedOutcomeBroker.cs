// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 每条发布都返回同一个结果的代理
/// </summary>
/// <remarks>
/// 记下收到的主题，一条消息一项，按发布顺序；不消费
/// </remarks>
/// <param name="outcome">每条消息的发布结果</param>
internal sealed class FixedOutcomeBroker(Result outcome) : IMessageBroker
{
    /// <summary>
    /// 收到的主题，一条消息一项，按发布顺序
    /// </summary>
    public List<string> Topics { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        Topics.Add(topic);
        return ValueTask.FromResult(outcome);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        Topics.AddRange(envelopes.Select(_ => topic));
        return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(_ => outcome)]);
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
