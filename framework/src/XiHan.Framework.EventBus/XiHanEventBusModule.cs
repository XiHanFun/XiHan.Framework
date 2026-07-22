// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Extensions.DependencyInjection;
using XiHan.Framework.Messaging;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 曦寒框架事件总线模块
/// </summary>
[DependsOn(
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanEventBusAbstractionsModule),
    typeof(XiHanMessagingModule),
    typeof(XiHanUowModule)
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
