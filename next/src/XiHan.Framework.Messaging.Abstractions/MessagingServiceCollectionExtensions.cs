// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Messaging.Abstractions.DomainEvents;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

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
    /// 登记一种领域事件（RFC-0009）。
    /// </summary>
    /// <remarks>
    /// 聚合记录的 <typeparamref name="TEvent"/> 在工作单元提交前发布到 <paramref name="topic"/>，CloudEvents <c>type</c> 为 <paramref name="type"/>，数据用 <paramref name="info"/> 序列化（源生成，不经反射），<paramref name="subject"/> 给出信封的 <c>subject</c>。首次调用同时注册 <see cref="IDomainEventDispatcher"/> 的实现 <see cref="DomainEventDispatcher"/>（作用域）。
    /// </remarks>
    public static IServiceCollection AddDomainEvent<TEvent>(this IServiceCollection services, string topic, string type, JsonTypeInfo<TEvent> info, Func<TEvent, string?>? subject = null)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(info);
        // 选择器按事件对象调用，登记表只认非泛型委托；强制转换在这里完成一次
        Func<IDomainEvent, string?>? selector = subject is null ? null : domainEvent => subject((TEvent)domainEvent);
        services.GetDomainEventRegistry().Add(new DomainEventRegistration(typeof(TEvent), topic, type, info, selector));
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }

    /// <summary>
    /// 取（必要时创建）领域事件登记表。
    /// </summary>
    /// <remarks>
    /// 以实例注册，多次调用返回同一对象。
    /// </remarks>
    public static DomainEventRegistry GetDomainEventRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(DomainEventRegistry) && descriptor.ImplementationInstance is DomainEventRegistry existing)
            {
                return existing;
            }
        }

        var registry = new DomainEventRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    /// <summary>
    /// 取（必要时创建）订阅表。
    /// </summary>
    public static MessageSubscriptions GetMessageSubscriptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 订阅表以实例注册，多次调用返回同一对象，特性与应用可以共同填充
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
