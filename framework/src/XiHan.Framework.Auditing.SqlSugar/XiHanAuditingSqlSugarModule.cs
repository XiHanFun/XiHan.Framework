// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;

namespace XiHan.Framework.Auditing.SqlSugar;

/// <summary>
/// 曦寒框架审计日志 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanAuditingSqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 写入器替换 <see cref="XiHanAuditingModule"/> 注册的空写入器。
/// </remarks>
[DependsOn(
    typeof(XiHanAuditingModule),
    typeof(XiHanDataModule)
)]
public class XiHanAuditingSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanAuditingSqlSugar();
    }
}
