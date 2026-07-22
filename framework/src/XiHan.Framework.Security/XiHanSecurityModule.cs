// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Security.Extensions.DependencyInjection;

namespace XiHan.Framework.Security;

/// <summary>
/// 曦寒框架安全模块
/// </summary>
public class XiHanSecurityModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加安全服务
        services.AddXiHanSecurityServices(config);
    }
}
