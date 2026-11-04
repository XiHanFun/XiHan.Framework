// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 记录发布次数的消息发布器桩
/// </summary>
/// <remarks>
/// 给了探针时每次批量发布都等到令牌取消（最多 30 秒），用来核对发布受外层截止时间约束
/// </remarks>
internal sealed class ProbePublisher(CancellationProbe? blockUntilCancelled = null) : IMessagePublisher
{
    /// <summary>
    /// 发布次数（按信封计）
    /// </summary>
    private int _published;

    /// <summary>
    /// 已发布的信封数
    /// </summary>
    public int Published => Volatile.Read(ref _published);

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _published);
        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        Interlocked.Add(ref _published, envelopes.Count);
        if (blockUntilCancelled is { } probe)
        {
            await probe.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        }

        return envelopes.Select(static _ => Result.Success()).ToList();
    }
}
