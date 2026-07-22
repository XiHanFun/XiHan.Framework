// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectMapping.Extensions.DependencyInjection;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 曦寒框架对象映射模块
/// </summary>
[DependsOn(
    typeof(XiHanLocalizationAbstractionsModule),
    typeof(XiHanValidationAbstractionsModule)
    )]
public class XiHanObjectMappingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanMapster();
    }
}
