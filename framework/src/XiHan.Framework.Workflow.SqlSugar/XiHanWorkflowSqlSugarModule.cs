// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Workflow.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.SqlSugar;

/// <summary>
/// 曦寒框架工作流 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanWorkflowSqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 存储替换工作流的进程内默认存储。
/// 配置节：<c>XiHan:Workflow:SqlSugar</c>。
/// </remarks>
[DependsOn(
    typeof(XiHanWorkflowModule),
    typeof(XiHanDataModule)
)]
public class XiHanWorkflowSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanWorkflowSqlSugar(services.GetConfiguration());
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.WarnIfWorkflowLockIsProcessLocal();
    }
}
