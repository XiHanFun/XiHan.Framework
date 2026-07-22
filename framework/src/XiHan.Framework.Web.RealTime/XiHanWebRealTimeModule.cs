// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.RealTime.Extensions;
using XiHan.Framework.Web.RealTime.Options;

namespace XiHan.Framework.Web.RealTime;

/// <summary>
/// 曦寒框架 Web 核心实时通信模块
/// </summary>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanAuthenticationModule)
    )]
public class XiHanWebRealTimeModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanSignalRWithJson(config);
    }
}
