﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreAuthenticationOAuthModule
// Guid:05cad840-07b0-411b-a0d2-f50027d733e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:49:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.Authentication.OAuth;

/// <summary>
/// 曦寒框架 Web 核心鉴权 OAuth 模块
/// </summary>
public class XiHanAspNetCoreAuthenticationOAuthModule : XiHanModule
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
