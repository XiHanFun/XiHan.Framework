// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.EventBus.RabbitMQ;

/// <summary>
/// 曦寒框架 RabbitMQ 分布式事件总线模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanRabbitMQEventBusModule))]</c> 即启用；
/// 该模块会以 <see cref="RabbitMQDistributedEventBus"/> 替换默认的本地分布式事件总线实现。
/// 配置节：<c>XiHan:EventBus:RabbitMQ</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanEventBusModule)
)]
public class XiHanRabbitMQEventBusModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.Configure<XiHanRabbitMQEventBusOptions>(
            config.GetSection(XiHanRabbitMQEventBusOptions.SectionName));
    }

    /// <summary>
    /// 应用初始化：建立与 RabbitMQ 的连接并启动消费者
    /// </summary>
    /// <param name="context"></param>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var bus = context.ServiceProvider.GetRequiredService<RabbitMQDistributedEventBus>();
        await bus.InitializeAsync();
    }
}
