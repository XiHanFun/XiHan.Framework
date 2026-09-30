// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.EventBus.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.EventBus.SqlSugar;

/// <summary>
/// 曦寒框架分布式事件总线 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanSqlSugarEventBusModule))]</c> 即启用。
/// 本模块以 SqlSugar 收发件箱替换默认的进程内收发件箱。
/// 配置节：<c>XiHan:EventBus:SqlSugar</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanEventBusModule),
    typeof(XiHanDataModule)
)]
public class XiHanSqlSugarEventBusModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanSqlSugarEventBus(services.GetConfiguration());
    }
}
