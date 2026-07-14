#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanRedisEventBusModule
// Guid:896e3fee-d42c-404d-a86c-097fc9f812b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.EventBus.Redis;

/// <summary>
/// 曦寒框架 Redis（Streams）分布式事件总线模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanRedisEventBusModule))]</c> 即启用；
/// 该模块会以 <see cref="RedisDistributedEventBus"/> 替换默认的本地分布式事件总线实现。
/// 配置节：<c>XiHan:EventBus:Redis</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanEventBusModule),
    typeof(XiHanCachingModule)
)]
public class XiHanRedisEventBusModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.Configure<XiHanRedisEventBusOptions>(
            config.GetSection(XiHanRedisEventBusOptions.SectionName));
    }

    /// <summary>
    /// 应用初始化：建立与 Redis 的连接并启动消费循环
    /// </summary>
    /// <param name="context"></param>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var bus = context.ServiceProvider.GetRequiredService<RedisDistributedEventBus>();
        await bus.InitializeAsync();
    }
}
