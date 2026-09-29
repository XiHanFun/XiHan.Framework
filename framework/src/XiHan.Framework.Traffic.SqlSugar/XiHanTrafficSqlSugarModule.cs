// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Traffic.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Traffic.SqlSugar;

/// <summary>
/// 曦寒框架流量治理 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanTrafficSqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 只读仓储替换 <see cref="XiHanTrafficModule"/> 注册的内存实现。
/// </remarks>
[DependsOn(
    typeof(XiHanTrafficModule),
    typeof(XiHanDataModule)
)]
public class XiHanTrafficSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        services.AddXiHanTrafficSqlSugar(configuration);
    }
}
