// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.Validation;

/// <summary>
/// 曦寒框架数据校验模块
/// </summary>
[DependsOn(
    typeof(XiHanValidationAbstractionsModule)
)]
public class XiHanValidationModule : XiHanModule
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
