// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Pipelines;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Workers;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Extensions.DependencyInjection;

/// <summary>
/// 曦寒审计日志服务集合扩展
/// </summary>
public static class XiHanAuditingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒审计日志基础设施（队列 + 后台消费者 + 采集管道 + 空写入器契约）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAuditing(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanAuditingLogQueueOptions>(
            configuration.GetSection(XiHanAuditingLogQueueOptions.SectionName));

        // 异步日志队列（有界 Channel）
        services.AddSingleton(typeof(ILogQueue<>), typeof(ChannelLogQueue<>));

        // 后台消费者：批量落库
        services.AddHostedService<AccessLogQueueWorker>();
        services.AddHostedService<OperationLogQueueWorker>();
        services.AddHostedService<ExceptionLogQueueWorker>();
        services.AddHostedService<ApiLogQueueWorker>();
        services.AddHostedService<LoginLogQueueWorker>();

        // 采集管道：按配置决定入队异步或同步写入
        services.AddScoped<IAccessLogPipeline, AccessLogPipeline>();
        services.AddScoped<IOperationLogPipeline, OperationLogPipeline>();
        services.AddScoped<IExceptionLogPipeline, ExceptionLogPipeline>();
        services.AddScoped<IApiLogPipeline, ApiLogPipeline>();
        services.AddScoped<ILoginLogPipeline, LoginLogPipeline>();

        // 写入器契约：默认空实现，应用侧用 Replace/自身注册覆盖以真正落库（TryAdd 不覆盖已注册）
        services.TryAddScoped<IAccessLogWriter, NullAccessLogWriter>();
        services.TryAddScoped<IOperationLogWriter, NullOperationLogWriter>();
        services.TryAddScoped<IExceptionLogWriter, NullExceptionLogWriter>();
        services.TryAddScoped<IApiLogWriter, NullApiLogWriter>();
        services.TryAddScoped<ILoginLogWriter, NullLoginLogWriter>();

        // 实体变更审计：默认上下文提供器 + 空 diff 写入器（应用侧覆盖）
        services.TryAddScoped<IEntityAuditContextProvider, DefaultEntityAuditContextProvider>();
        services.TryAddScoped<IEntityDiffLogWriter, NullEntityDiffLogWriter>();

        return services;
    }
}
