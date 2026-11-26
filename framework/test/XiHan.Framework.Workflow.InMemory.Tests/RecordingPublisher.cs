// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 记下工作流发布的信封的发布者
/// </summary>
/// <remarks>
/// 线程安全：引擎在调用里、轮询器在后台发布；每一项都成功
/// </remarks>
internal sealed class RecordingPublisher : IMessagePublisher
{
    /// <summary>
    /// 已发布的信封，按发布顺序
    /// </summary>
    private readonly List<MessageEnvelope> _envelopes = [];

    /// <summary>
    /// 已发布的信封的快照
    /// </summary>
    public IReadOnlyList<MessageEnvelope> Envelopes
    {
        get
        {
            lock (_envelopes)
            {
                return [.. _envelopes];
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        => (await PublishManyAsync(topic, [envelope], cancellationToken))[0];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        lock (_envelopes)
        {
            _envelopes.AddRange(envelopes);
        }

        return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(static _ => Result.Success())]);
    }

    /// <summary>
    /// 等到发布了给定类型的信封
    /// </summary>
    /// <remarks>
    /// 超时抛出 <see cref="TimeoutException"/>
    /// </remarks>
    public async Task<MessageEnvelope> WaitAsync(string type, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (Envelopes.FirstOrDefault(envelope => envelope.Type == type) is { } found)
            {
                return found;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"No envelope of type '{type}' was published within {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
}
