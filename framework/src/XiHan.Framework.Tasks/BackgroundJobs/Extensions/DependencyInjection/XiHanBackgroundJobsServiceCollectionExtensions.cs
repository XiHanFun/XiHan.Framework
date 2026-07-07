#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBackgroundJobsServiceCollectionExtensions
// Guid:a4dfe019-9ab9-4d00-b64e-876b483488c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;

namespace XiHan.Framework.Tasks.BackgroundJobs.Extensions.DependencyInjection;

/// <summary>
/// 曦寒后台作业服务集合扩展
/// </summary>
public static class XiHanBackgroundJobsServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒后台作业（管理器 + 轮询 Worker + 内存存储默认 + 作业处理器自动发现）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BackgroundJobWorkerOptions>(
            configuration.GetSection(BackgroundJobWorkerOptions.SectionName));

        // 核心服务（可被应用侧替换：如替换 IBackgroundJobStore 为持久化/分布式实现）
        services.TryAddSingleton<IBackgroundJobSerializer, BackgroundJobSerializer>();
        services.TryAddSingleton<IBackgroundJobStore, InMemoryBackgroundJobStore>();
        services.TryAddTransient<IBackgroundJobManager, BackgroundJobManager>();
        services.TryAddTransient<IBackgroundJobExecuter, BackgroundJobExecuter>();

        // 轮询 Worker
        services.AddHostedService<BackgroundJobWorker>();

        // 自动发现作业处理器（实现 IAsyncBackgroundJob<> 的非抽象类型）→ 登记进注册表
        RegisterJobDiscovery(services);

        return services;
    }

    /// <summary>
    /// 通过注册钩子自动收集作业处理器类型并填充注册表
    /// </summary>
    /// <param name="services">服务集合</param>
    private static void RegisterJobDiscovery(IServiceCollection services)
    {
        var jobTypes = new List<Type>();

        services.OnRegistered(context =>
        {
            if (BackgroundJobArgsHelper.IsBackgroundJob(context.ImplementationType))
            {
                jobTypes.Add(context.ImplementationType);
            }
        });

        services.Configure<BackgroundJobOptions>(options =>
        {
            foreach (var jobType in jobTypes)
            {
                options.AddJob(jobType);
            }
        });
    }
}
