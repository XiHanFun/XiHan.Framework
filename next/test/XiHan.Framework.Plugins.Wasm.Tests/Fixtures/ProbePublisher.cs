// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 记录发布次数的消息发布器桩。
/// </summary>
internal sealed class ProbePublisher : IMessagePublisher
{
    /// <summary>
    /// 发布次数（按信封计）。
    /// </summary>
    private int _published;

    /// <summary>
    /// 已发布的信封数。
    /// </summary>
    public int Published => Volatile.Read(ref _published);

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _published);
        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        Interlocked.Add(ref _published, envelopes.Count);
        return ValueTask.FromResult<IReadOnlyList<Result>>(envelopes.Select(static _ => Result.Success()).ToList());
    }
}
