#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanKafkaEventBusModule
// Guid:acaeccf5-43e2-46cf-ad06-8f6489a32243
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.EventBus.Kafka;

/// <summary>
/// 曦寒框架 Kafka 分布式事件总线模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanKafkaEventBusModule))]</c> 即启用；
/// 该模块会以 <see cref="KafkaDistributedEventBus"/> 替换默认的本地分布式事件总线实现。
/// 配置节：<c>XiHan:EventBus:Kafka</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanEventBusModule)
)]
public class XiHanKafkaEventBusModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.Configure<XiHanKafkaEventBusOptions>(
            config.GetSection(XiHanKafkaEventBusOptions.SectionName));
    }

    /// <summary>
    /// 应用初始化：建立与 Kafka 的连接并启动消费循环
    /// </summary>
    /// <param name="context"></param>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var bus = context.ServiceProvider.GetRequiredService<KafkaDistributedEventBus>();
        await bus.InitializeAsync();
    }
}
