// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Security.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Security.SqlSugar;

/// <summary>
/// 曦寒框架安全模块 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanSecuritySqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 存储替换 <see cref="XiHanSecurityModule"/> 注册的内存实现。
/// </remarks>
[DependsOn(
    typeof(XiHanSecurityModule),
    typeof(XiHanDataModule)
)]
public class XiHanSecuritySqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanSecuritySqlSugar();
    }
}
