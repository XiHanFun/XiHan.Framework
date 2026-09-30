// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;

namespace XiHan.Framework.Authorization.SqlSugar;

/// <summary>
/// 曦寒框架授权 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanAuthorizationSqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 实现替换 <see cref="XiHanAuthorizationModule"/> 注册的内存存储。
/// </remarks>
[DependsOn(
    typeof(XiHanAuthorizationModule),
    typeof(XiHanDataModule)
)]
public class XiHanAuthorizationSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanAuthorizationSqlSugar();
    }
}
