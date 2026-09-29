// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;

namespace XiHan.Framework.Authentication.SqlSugar;

/// <summary>
/// 曦寒框架认证存储 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanAuthenticationSqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 存储替换认证模块的用户、刷新令牌与第三方登录的内存存储。
/// 配置节：<c>XiHan:Authentication:SqlSugar</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanAuthenticationModule),
    typeof(XiHanDataModule)
)]
public class XiHanAuthenticationSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanAuthenticationSqlSugar(services.GetConfiguration());
    }
}
