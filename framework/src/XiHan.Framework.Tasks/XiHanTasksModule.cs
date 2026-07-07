#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTasksModule
// Guid:d48648fc-a480-49be-8c28-3d5b486f8614
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:28:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
