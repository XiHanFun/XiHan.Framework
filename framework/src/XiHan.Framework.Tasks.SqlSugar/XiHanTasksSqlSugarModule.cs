// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Tasks.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Tasks.SqlSugar;

/// <summary>
/// 曦寒框架任务 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanTasksSqlSugarModule))]</c> 即启用。
/// 配置节：<c>XiHan:Tasks:SqlSugar</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanTasksModule),
    typeof(XiHanDataModule)
)]
public class XiHanTasksSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanTasksSqlSugar(services.GetConfiguration());
    }
}
