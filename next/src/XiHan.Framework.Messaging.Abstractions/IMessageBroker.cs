// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 一次投递：主题、信封与第几次尝试（从 1 起）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageDelivery(string Topic, MessageEnvelope Envelope, int Attempt)
{
    /// <summary>
    /// 主题。
    /// </summary>
    public string Topic { get; } = TopicName.Validate(Topic);

    /// <summary>
    /// 信封。
    /// </summary>
    public MessageEnvelope Envelope { get; } = Envelope ?? throw new ArgumentNullException(nameof(Envelope));

    /// <summary>
    /// 第几次尝试。
    /// </summary>
    public int Attempt { get; } = Attempt >= 1 ? Attempt : throw new ArgumentOutOfRangeException(nameof(Attempt), Attempt, "尝试次数从 1 起。");
}

/// <summary>
/// 把一次投递交给处理器；返回失败表示投递失败，由代理重试或死信。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public delegate ValueTask<Result> MessageDispatch(MessageDelivery delivery, CancellationToken cancellationToken);

/// <summary>
/// 消息传输，Provider 实现它：发布、按主题消费、至少一次投递、按 <see cref="MessagingOptions.MaxAttempts"/> 重试后死信。
/// 基础设施失败抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的 <see cref="FrameworkException"/>；队列已满返回 <see cref="MessagingErrors.QueueFull"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageBroker
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// 开始消费这些主题，每条消息调用 <paramref name="dispatch"/>。
    /// </summary>
    Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken);

    /// <summary>
    /// 停止消费并等待进行中的投递结束。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 应用发布消息的入口。主题不合法返回 <see cref="MessagingErrors.InvalidTopic"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessagePublisher
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);
}

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

/// <summary>
/// 消息处理器。返回失败或抛出异常都算投递失败。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageHandler
{
    /// <summary>
    /// 处理。
    /// </summary>
    ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 主题到处理器类型的订阅表。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessageSubscriptions
{
    private readonly Dictionary<string, List<Type>> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// 已订阅的主题。
    /// </summary>
    public IReadOnlyCollection<string> Topics => _handlers.Keys;

    /// <summary>
    /// 主题的处理器类型，按订阅顺序。
    /// </summary>
    public IReadOnlyList<Type> HandlersFor(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return _handlers.TryGetValue(topic, out var handlers) ? handlers : [];
    }

    /// <summary>
    /// 订阅。
    /// </summary>
    public MessageSubscriptions Add(string topic, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        TopicName.Validate(topic);
        if (!_handlers.TryGetValue(topic, out var handlers))
        {
            handlers = [];
            _handlers[topic] = handlers;
        }

        if (!handlers.Contains(handlerType))
        {
            handlers.Add(handlerType);
        }

        return this;
    }
}

/// <summary>
/// 处理器订阅扩展。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// 订阅主题：处理器注册为作用域服务，每条消息在新作用域内解析。
    /// </summary>
    public static IServiceCollection AddMessageHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services, string topic)
        where THandler : class, IMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<THandler>();
        services.GetMessageSubscriptions().Add(topic, typeof(THandler));
        return services;
    }

    /// <summary>
    /// 取（必要时创建）订阅表。
    /// </summary>
    public static MessageSubscriptions GetMessageSubscriptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(MessageSubscriptions) && descriptor.ImplementationInstance is MessageSubscriptions existing)
            {
                return existing;
            }
        }

        var subscriptions = new MessageSubscriptions();
        services.AddSingleton(subscriptions);
        return subscriptions;
    }
}
