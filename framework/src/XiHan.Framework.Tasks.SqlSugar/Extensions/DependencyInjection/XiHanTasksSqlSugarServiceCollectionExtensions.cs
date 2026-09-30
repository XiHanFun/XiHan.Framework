// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Options;
using XiHan.Framework.Tasks.SqlSugar.ScheduledJobs;

namespace XiHan.Framework.Tasks.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 任务 SqlSugar 存储服务集合扩展
/// </summary>
public static class XiHanTasksSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 存储替换任务模块的进程内存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanTasksSqlSugar(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<XiHanTasksSqlSugarOptions>()
            .Bind(configuration.GetSection(XiHanTasksSqlSugarOptions.SectionName))
            .Validate(
                options => options.MaxClaimBatchSize > 0 && options.BackgroundJobLeaseTimeout > TimeSpan.Zero,
                "任务 SqlSugar 存储配置无效：MaxClaimBatchSize 与 BackgroundJobLeaseTimeout 必须大于零。")
            .ValidateOnStart();

        services.TryAddSingleton<TasksHostClientAccessor>();
        services.Replace(ServiceDescriptor.Singleton<IBackgroundJobStore, SqlSugarBackgroundJobStore>());
        services.Replace(ServiceDescriptor.Singleton<IJobStore, SqlSugarJobStore>());

        return services;
    }
}
