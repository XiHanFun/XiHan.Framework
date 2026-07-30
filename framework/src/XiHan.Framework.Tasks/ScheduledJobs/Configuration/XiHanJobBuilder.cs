// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Configuration;

/// <summary>
/// 曦寒任务调度构建器
/// </summary>
public class XiHanJobBuilder
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanJobBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 使用自定义存储
    /// </summary>
    public XiHanJobBuilder UseStore<TStore>() where TStore : class, IJobStore
    {
        Services.AddSingleton<IJobStore, TStore>();
        return this;
    }

    /// <summary>
    /// 使用自定义锁提供者
    /// </summary>
    public XiHanJobBuilder UseLockProvider<TLockProvider>() where TLockProvider : class, IJobLockProvider
    {
        Services.AddSingleton<IJobLockProvider, TLockProvider>();
        return this;
    }

    /// <summary>
    /// 添加中间件
    /// </summary>
    public XiHanJobBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IJobMiddleware
    {
        Services.AddSingleton<IJobMiddleware, TMiddleware>();
        return this;
    }

    /// <summary>
    /// 添加任务
    /// </summary>
    public XiHanJobBuilder AddJob<TJob>() where TJob : class, IJobWorker
    {
        Services.AddTransient<TJob>();
        return this;
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    public XiHanJobBuilder Configure(Action<XiHanJobOptions> configureOptions)
    {
        Services.Configure(configureOptions);
        return this;
    }
}
