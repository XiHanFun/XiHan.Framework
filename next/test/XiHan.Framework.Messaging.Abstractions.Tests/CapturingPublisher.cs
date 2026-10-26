// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 捕获发布的发布者：记录每次批量发布的主题与信封；设置 <see cref="Failure"/> 后每一项都返回该失败。
/// </summary>
internal sealed class CapturingPublisher : IMessagePublisher
{
    /// <summary>
    /// 已记录的批次。
    /// </summary>
    private readonly List<(string Topic, IReadOnlyList<MessageEnvelope> Envelopes)> _batches = [];

    /// <summary>
    /// 已记录的批次，按发布顺序。
    /// </summary>
    public IReadOnlyList<(string Topic, IReadOnlyList<MessageEnvelope> Envelopes)> Batches => _batches;

    /// <summary>
    /// 设置后每一项都返回该失败。
    /// </summary>
    public Error? Failure { get; set; }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        => (await PublishManyAsync(topic, [envelope], cancellationToken))[0];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        _batches.Add((topic, envelopes));
        var outcome = Failure is { } failure ? Result.Failure(failure) : Result.Success();
        return ValueTask.FromResult<IReadOnlyList<Result>>(envelopes.Select(_ => outcome).ToList());
    }
}
