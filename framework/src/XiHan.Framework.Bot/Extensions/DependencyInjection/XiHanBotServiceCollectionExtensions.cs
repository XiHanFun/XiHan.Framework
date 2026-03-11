#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotServiceCollectionExtensions
// Guid:5f1b2e7a-ebea-4266-9348-20029299b169
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:47:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Clients;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Strategy;
using XiHan.Framework.Bot.Template;
using XiHan.Framework.Templating.Extensions.DependencyInjection;

namespace XiHan.Framework.Bot.Extensions.DependencyInjection;

/// <summary>
/// Bot 服务注册扩展
/// </summary>
public static class XiHanBotServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Bot 服务
    /// </summary>
    public static IServiceCollection AddXiHanBot(this IServiceCollection services, Action<BotBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddXiHanTemplating();
        services.AddOptions<XiHanBotOptions>();

        services.TryAddSingleton<BotProviderManager>();
        services.TryAddSingleton<BotDispatcher>();
        services.TryAddSingleton<IBotClient, BotClient>();
        services.TryAddSingleton<IBotTemplateEngine, BotTemplateEngine>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotStrategy, BroadcastStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotStrategy, FailoverStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotStrategy, PriorityStrategy>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotPipeline, LoggingPipeline>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotPipeline, EnvironmentFilterPipeline>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotPipeline, RetryPipeline>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotPipeline, RateLimitPipeline>());

        var builder = new BotBuilder(services);
        configure?.Invoke(builder);

        return services;
    }
}
