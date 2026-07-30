// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Security;

namespace XiHan.Framework.Authentication;

/// <summary>
/// 曦寒框架认证模块
/// </summary>
[DependsOn(
    typeof(XiHanSecurityModule)
    )]
public class XiHanAuthenticationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加认证服务
        services.AddXiHanAuthentication(config);
    }
}
