#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanEventBusModule
// Guid:33286838-2730-4c0a-a480-0aa34d45b21c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:34:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 曦寒框架事件总线模块
/// </summary>
[DependsOn(
    typeof(XiHanMultiTenancyAbstractionsModule),
    typeof(XiHanDistributedIdsModule)
    )]
public class XiHanEventBusModule : XiHanModule
{
    /// <summary>
    /// 服务配置前，异步
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加事件总线服务
        services.AddXiHanEventBus(config);
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
    }

    /// <summary>
    /// 应用初始化，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        //await context.AddBackgroundWorkerAsync<OutboxSenderManager>();
        //await context.AddBackgroundWorkerAsync<InboxProcessManager>();
    }
}
