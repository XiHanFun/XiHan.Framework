// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 直接发布到代理
/// </summary>
/// <remarks>
/// 在调用内发布时先从当前调用补齐信封缺失的扩展（<c>traceparent</c>、租户、主体、关联、区域，RFC-0009「信封」），信封已有的扩展保持不变
/// </remarks>
/// <param name="broker">目标代理</param>
/// <param name="invocation">当前调用的访问器；为 <see langword="null"/> 时不补扩展</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DirectPublisher(IMessageBroker broker, IInvocationAccessor? invocation = null) : IMessagePublisher
{
    /// <summary>
    /// 目标代理
    /// </summary>
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));

    /// <summary>
    /// 当前调用的访问器
    /// </summary>
    /// <remarks>
    /// 发布时从它取调用上下文补齐扩展
    /// </remarks>
    private readonly IInvocationAccessor? _invocation = invocation;

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return TopicName.IsValid(topic)
            ? _broker.PublishAsync(topic, InvocationEnvelopes.Enrich(envelope, _invocation), cancellationToken)
            : ValueTask.FromResult<Result>(InvalidTopic(topic));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        if (!TopicName.IsValid(topic))
        {
            return ValueTask.FromResult(Repeat(InvalidTopic(topic), envelopes.Count));
        }

        return envelopes.Count == 0
            ? ValueTask.FromResult<IReadOnlyList<Result>>([])
            : _broker.PublishManyAsync(topic, InvocationEnvelopes.Enrich(envelopes, _invocation), cancellationToken);
    }

    /// <summary>
    /// 构造主题不合法的错误
    /// </summary>
    internal static Error InvalidTopic(string topic) => Error.Validation(MessagingErrors.InvalidTopic, MessagingMessages.InvalidTopic, MessageArguments.Of(("topic", topic)));

    /// <summary>
    /// 同一结果重复给每一项（主题不合法时整批同一错误）
    /// </summary>
    internal static IReadOnlyList<Result> Repeat(Result result, int count)
    {
        var results = new Result[count];
        Array.Fill(results, result);
        return results;
    }
}
