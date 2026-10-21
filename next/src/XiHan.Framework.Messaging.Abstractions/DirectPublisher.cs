// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 直接发布到代理。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DirectPublisher(IMessageBroker broker) : IMessagePublisher
{
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return TopicName.IsValid(topic)
            ? _broker.PublishAsync(topic, envelope, cancellationToken)
            : ValueTask.FromResult<Result>(Error.Validation(MessagingErrors.InvalidTopic, $"主题 '{topic}' 不合法。"));
    }
}
