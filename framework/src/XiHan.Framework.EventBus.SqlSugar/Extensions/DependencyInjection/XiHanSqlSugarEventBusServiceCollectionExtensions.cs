// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Inbox;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.EventBus.SqlSugar.Outbox;

namespace XiHan.Framework.EventBus.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 事件总线 SqlSugar 存储服务集合扩展
/// </summary>
public static class XiHanSqlSugarEventBusServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 收发件箱替换默认的进程内收发件箱
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanSqlSugarEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<XiHanSqlSugarEventBoxOptions>()
            .Bind(configuration.GetSection(XiHanSqlSugarEventBoxOptions.SectionName))
            .Validate(
                options => options.ClaimTimeout > TimeSpan.Zero && options.InboxRetentionPeriod > TimeSpan.Zero,
                "事件收发件箱配置无效：ClaimTimeout 与 InboxRetentionPeriod 必须大于零。")
            .ValidateOnStart();

        services.Configure<XiHanDistributedEventBusOptions>(options =>
        {
            options.Outboxes.Configure(config => config.ImplementationType = typeof(SqlSugarEventOutbox));
            options.Inboxes.Configure(config => config.ImplementationType = typeof(SqlSugarEventInbox));
        });

        services.TryAddScoped<SqlSugarEventOutbox>();
        services.Replace(ServiceDescriptor.Scoped<IEventOutbox, SqlSugarEventOutbox>());

        services.TryAddScoped<SqlSugarEventInbox>();
        services.Replace(ServiceDescriptor.Scoped<IEventInbox, SqlSugarEventInbox>());

        return services;
    }
}
