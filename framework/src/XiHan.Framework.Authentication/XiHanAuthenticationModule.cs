#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthenticationModule
// Guid:7aecf557-92ae-475f-bbd9-c667564c59a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:49:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// 曦寒框架 Web 核心鉴权 Jwt 模块
/// </summary>
public class XiHanAuthenticationModule : XiHanModule
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
