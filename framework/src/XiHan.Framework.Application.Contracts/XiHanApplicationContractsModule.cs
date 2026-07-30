// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Domain.Shared;

namespace XiHan.Framework.Application.Contracts;

/// <summary>
/// 曦寒框架应用层契约模块
/// </summary>
[DependsOn(
    typeof(XiHanDomainSharedModule))]
public class XiHanApplicationContractsModule : XiHanModule
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
