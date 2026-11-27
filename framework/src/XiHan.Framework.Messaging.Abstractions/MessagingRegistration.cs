// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// Provider 共用的装配
/// </summary>
/// <remarks>
/// 选项、实例标识、派发器、直接发布者与运行时，以及配置节 <c>XiHan:Messaging:DataSchemas</c> 登记的数据模式（ADR-0332）。Provider 的特性先注册 <see cref="IMessageBroker"/> 再调用
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingRegistration
{
    /// <summary>
    /// 注册消息子系统的公共服务
    /// </summary>
    /// <remarks>
    /// 配置的数据模式（<see cref="MessagingOptions.DataSchemas"/>）登记进 <see cref="MessageDataSchemas"/>：与代码登记的同一个 <c>type</c> 值不同时抛出 <see cref="ArgumentException"/>，装配失败；没有配置时不建这张表
    /// </remarks>
    public static MessagingOptions Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = MessagingOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        AddConfiguredDataSchemas(context.Services, options);
        context.Services.TryAddSingleton<MessagingInstance>();
        context.Services.GetMessageSubscriptions();
        context.Services.TryAddSingleton<MessageDispatcher>();
        context.Services.TryAddSingleton<MessagingRuntime>();
        context.Services.TryAddScoped<IMessagePublisher, DirectPublisher>();
        return options;
    }

    /// <summary>
    /// 把配置的数据模式登记进数据模式表
    /// </summary>
    /// <remarks>
    /// 与 <see cref="MessagingServiceCollectionExtensions.AddMessageDataSchema"/> 同一张表、同一条重复规则：同值只记一次（Provider 装配两次也一样），异值装配失败，配置不顶掉代码登记的（RFC-0009「框架事件的数据模式」，ADR-0332）
    /// </remarks>
    private static void AddConfiguredDataSchemas(IServiceCollection services, MessagingOptions options)
    {
        // 没有配置就不建表：容器里没有它时三类信封与以前完全相同
        if (options.DataSchemas.Count == 0)
        {
            return;
        }

        var schemas = services.GetMessageDataSchemas();
        foreach (var (type, dataSchema) in options.DataSchemas)
        {
            try
            {
                schemas.Add(type, dataSchema);
            }
            catch (ArgumentException exception)
            {
                // 值已在读配置时核对过，到这里的只有与代码登记的冲突：指出是配置里的哪一项
                throw new ArgumentException($"'{MessagingOptions.ConfigurationSection}:DataSchemas' cannot register event type '{type}': {exception.Message}", exception);
            }
        }
    }
}
