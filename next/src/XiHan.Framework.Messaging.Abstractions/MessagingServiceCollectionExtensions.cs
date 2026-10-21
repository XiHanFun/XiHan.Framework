// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
