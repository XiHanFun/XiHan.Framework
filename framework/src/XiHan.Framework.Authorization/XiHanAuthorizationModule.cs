// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication;
using XiHan.Framework.Authorization.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Authorization;

/// <summary>
/// 曦寒框架授权模块
/// </summary>
[DependsOn(
    typeof(XiHanAuthenticationModule)
    )]
public class XiHanAuthorizationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加授权服务
        services.AddXiHanAuthorization(config);
    }
}
