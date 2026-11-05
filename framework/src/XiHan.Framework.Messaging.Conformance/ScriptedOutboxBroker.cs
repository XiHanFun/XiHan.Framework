// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 按脚本应答的代理，供发件箱套件使用
/// </summary>
/// <remarks>
/// 每次批量发布先记一次调用（这一批的事件 ID 以逗号连接），再交给 <see cref="Respond"/>：它返回逐项结果，或者直接抛出。投递循环只用批量发布，单条发布抛出 <see cref="NotSupportedException"/>；不消费。投递循环逐轮串行调用，调用记录不加锁
/// </remarks>
/// <param name="respond">应答：给定主题与这一批信封，返回逐项结果或抛出</param>
internal sealed class ScriptedOutboxBroker(Func<string, IReadOnlyList<MessageEnvelope>, IReadOnlyList<Result>> respond) : IMessageBroker
{
    /// <summary>
    /// 调用记录
    /// </summary>
    private readonly List<string> _calls = [];

    /// <summary>
    /// 每次批量发布的事件 ID（逗号连接），按调用顺序
    /// </summary>
    /// <remarks>
    /// 抛出的调用也在内
    /// </remarks>
    public IReadOnlyList<string> Calls => [.. _calls];

    /// <summary>
    /// 应答
    /// </summary>
    /// <remarks>
    /// 用例中途可以换掉，模拟代理恢复
    /// </remarks>
    public Func<string, IReadOnlyList<MessageEnvelope>, IReadOnlyList<Result>> Respond { get; set; } = respond ?? throw new ArgumentNullException(nameof(respond));

    /// <summary>
    /// 逐项成功的应答结果
    /// </summary>
    public static IReadOnlyList<Result> Succeeded(IReadOnlyList<MessageEnvelope> envelopes) => [.. envelopes.Select(static _ => Result.Success())];

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        => throw new NotSupportedException("The outbox dispatcher publishes in batches only.");

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        _calls.Add(string.Join(',', envelopes.Select(static e => e.Id)));
        return ValueTask.FromResult(Respond(topic, envelopes));
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
