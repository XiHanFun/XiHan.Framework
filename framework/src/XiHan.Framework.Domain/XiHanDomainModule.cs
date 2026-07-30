// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Domain.Shared;

namespace XiHan.Framework.Domain;

/// <summary>
/// 曦寒框架领域驱动设计模块
/// </summary>
[DependsOn(
    typeof(XiHanDomainSharedModule)
    )]
public class XiHanDomainModule : XiHanModule
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
