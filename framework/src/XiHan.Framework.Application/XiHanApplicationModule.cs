// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain;
using XiHan.Framework.Logging;
using XiHan.Framework.ObjectMapping;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Application;

/// <summary>
/// 曦寒框架领域驱动应用模块
/// </summary>
[DependsOn(
    typeof(XiHanLoggingModule),
    typeof(XiHanApplicationContractsModule),
    typeof(XiHanDomainModule),
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanObjectMappingModule)
    )]
public class XiHanApplicationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
    }
}
