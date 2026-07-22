// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Core;

/// <summary>
/// 曦寒框架 Web 核心模块
/// </summary>
public class XiHanWebCoreModule : XiHanModule
{
    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        var xihanHostEnvironment = services.GetSingletonInstance<IXiHanHostEnvironment>();
        if (xihanHostEnvironment.EnvironmentName.IsNullOrWhiteSpace())
        {
            xihanHostEnvironment.EnvironmentName = services.GetHostingEnvironment().EnvironmentName;
        }
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanWebCore(config);
    }
}
