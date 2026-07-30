// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Caching;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Configuration;
using XiHan.Framework.Tasks.ScheduledJobs.Extensions.DependencyInjection;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks;

/// <summary>
/// 曦寒框架任务模块
/// </summary>
[DependsOn(
    typeof(XiHanCachingModule),
    typeof(XiHanMultiTenancyAbstractionsModule),
    typeof(XiHanMultiTenancyModule),
    typeof(XiHanTimingModule)
    )]
public class XiHanTasksModule : XiHanModule
{
    /// <summary>
    /// 服务配置前（挂载作业处理器自动发现钩子，须早于业务模块的约定注册）
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 注册后台作业（fire-and-forget 一次性作业管理器 + 轮询 Worker + 内存存储默认）
        services.AddXiHanBackgroundJobs(config);
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 注册任务调度服务
        services.AddXiHanTasks(config);
    }
}
