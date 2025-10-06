#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScheduledJobServiceCollectionExtensions
// Guid:7b8c9d0e-1f2a-3b4c-5d6e-7f8a9b0c1d2e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:36:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using XiHan.Framework.Tasks.ScheduledJobs.Attributes;

namespace XiHan.Framework.Tasks.ScheduledJobs.Extensions;

/// <summary>
/// 调度任务服务集合扩展
/// </summary>
public static class ScheduledJobServiceCollectionExtensions
{
    /// <summary>
    /// 添加调度任务服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddScheduledJobs(
        this IServiceCollection services,
        Action<XiHanScheduledJobOptions>? configureOptions = null)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<XiHanScheduledJobOptions>(options => { });
        }

        // 注册任务管理器
        services.TryAddSingleton<IScheduledJobManager, XiHanScheduledJobManager>();

        // 注册托管服务
        services.AddHostedService<ScheduledJobHostedService>();

        return services;
    }

    /// <summary>
    /// 添加调度任务
    /// </summary>
    /// <typeparam name="TJob">任务类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddScheduledJob<TJob>(this IServiceCollection services)
        where TJob : class, IJob
    {
        services.AddTransient<TJob>();
        return services;
    }

    /// <summary>
    /// 自动注册所有带有 ScheduledJobAttribute 特性的任务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">要扫描的程序集（默认为调用程序集）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddScheduledJobsFromAssemblies(
        this IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [System.Reflection.Assembly.GetCallingAssembly()];
        }

        foreach (var assembly in assemblies)
        {
            var jobTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract &&
                            t.GetCustomAttributes(typeof(ScheduledJobAttribute), false).Any() &&
                            typeof(IJob).IsAssignableFrom(t));

            foreach (var jobType in jobTypes)
            {
                services.AddTransient(jobType);
            }
        }

        return services;
    }
}
