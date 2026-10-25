// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 应用发布消息的入口。主题不合法返回 <see cref="MessagingErrors.InvalidTopic"/>。
/// 批量发布按 <c>spec/conventions/batch-first.md</c>：结果与入参顺序一一对应，单条是它的特例，空集合不触代理。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessagePublisher
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// 批量发布到同一主题，一次交给代理；同一主题内保持入参顺序，重复 <see cref="MessageEnvelope.Id"/> 照发（去重是消费端的事）。
    /// 主题不合法时每一项都是 <see cref="MessagingErrors.InvalidTopic"/>。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken);
}
