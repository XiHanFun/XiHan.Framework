#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWorkflowModule
// Guid:c48e17f2-0b95-4d63-a2e8-751c09d6b3f4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:27:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Caching;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Script;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow;

/// <summary>
/// 曦寒框架工作流模块
/// </summary>
[DependsOn(
    typeof(XiHanCachingModule),
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanEventBusModule),
    typeof(XiHanMultiTenancyModule),
    typeof(XiHanMultiTenancyAbstractionsModule),
    typeof(XiHanScriptModule),
    typeof(XiHanTimingModule),
    typeof(XiHanWorkflowAbstractionsModule)
    )]
public class XiHanWorkflowModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanWorkflow(config);
    }
}
