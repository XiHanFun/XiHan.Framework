#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTasksModule
// Guid:d48648fc-a480-49be-8c28-3d5b486f8614
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:28:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Tasks.ScheduledJobs.Extensions.DependencyInjection;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks;

/// <summary>
/// 曦寒框架任务模块
/// </summary>
[DependsOn(
    typeof(XiHanTimingModule)
    )]
public class XiHanTasksModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 注册任务调度服务
        services.AddXiHanTasks(options =>
        {
            // 默认配置
            options.Enabled = true;
            options.AutoDiscoverJobs = true;
            options.EnableMetrics = true;
        });
    }
}
