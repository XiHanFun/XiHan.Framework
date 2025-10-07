#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanJobBuilder.cs
// Guid:f43035be-dabb-46be-a447-7fb0fda3711c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:13:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    public XiHanJobBuilder AddJob<TJob>() where TJob : class, IJob
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
