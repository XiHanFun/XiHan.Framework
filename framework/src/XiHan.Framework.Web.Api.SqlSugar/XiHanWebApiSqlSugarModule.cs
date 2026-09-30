// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Web.Api.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Api.SqlSugar;

/// <summary>
/// 曦寒框架 Web API 幂等 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块中 DependsOn 本模块即启用，会把默认的进程内幂等存储换成 SqlSugar 存储。
/// </remarks>
[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanDataModule)
)]
public class XiHanWebApiSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanWebApiSqlSugar(config);
    }
}
