// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 按脚本给出逐项结果的代理
/// </summary>
/// <remarks>
/// 每次批量发布记一次调用（<see cref="Calls"/>），成功交出的事件 ID 记进 <see cref="Published"/>；<see cref="Outcome"/> 决定每一项的结果，<see cref="Unreachable"/> 时整批抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的异常，<see cref="Throws"/> 为这一批里任一信封给出异常时整批抛出它。不消费
/// </remarks>
internal sealed class ScriptedBroker : IMessageBroker
{
    /// <summary>
    /// 发布过的事件 ID，按发布顺序
    /// </summary>
    /// <remarks>
    /// 只含没有抛出的调用里的
    /// </remarks>
    public List<string> Published { get; } = [];

    /// <summary>
    /// 每次批量发布的事件 ID（逗号连接），按调用顺序
    /// </summary>
    /// <remarks>
    /// 抛出的调用也在内
    /// </remarks>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// 每一项的结果
    /// </summary>
    /// <remarks>
    /// 缺省全部成功
    /// </remarks>
    public Func<MessageEnvelope, Result> Outcome { get; set; } = static _ => Result.Success();

    /// <summary>
    /// 让整批抛出的信封
    /// </summary>
    /// <remarks>
    /// 返回非空即以它整批抛出，模拟某条消息让代理整批失败（超过消息大小上限、主题不存在等）；缺省都不抛
    /// </remarks>
    public Func<MessageEnvelope, Exception?> Throws { get; set; } = static _ => null;

    /// <summary>
    /// 是否模拟代理不可达
    /// </summary>
    public bool Unreachable { get; set; }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        => (await PublishManyAsync(topic, [envelope], cancellationToken).ConfigureAwait(false))[0];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        Calls.Add(string.Join(',', envelopes.Select(static e => e.Id)));
        if (Unreachable)
        {
            throw new FrameworkException(MessagingErrors.BrokerUnavailable, "The scripted broker is unreachable.");
        }

        if (envelopes.Select(Throws).FirstOrDefault(static e => e is not null) is { } exception)
        {
            throw exception;
        }

        Published.AddRange(envelopes.Select(static e => e.Id));
        return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(Outcome)]);
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
