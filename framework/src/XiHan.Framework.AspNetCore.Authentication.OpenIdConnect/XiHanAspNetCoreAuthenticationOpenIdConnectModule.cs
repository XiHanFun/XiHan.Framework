#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreAuthenticationOpenIdConnectModule
// Guid:1d030224-5bed-4459-9485-693089413b65
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:50:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// 曦寒框架 Web 核心鉴权 OpenIdConnect 模块
/// </summary>
public class XiHanAspNetCoreAuthenticationOpenIdConnectModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        _ = services.AddAuthorization();
    }
}
