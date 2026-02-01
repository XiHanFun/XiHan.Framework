#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthenticationModule
// Guid:7aecf557-92ae-475f-bbd9-c667564c59a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:49:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Authentication.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Authentication;

/// <summary>
/// 曦寒框架认证模块
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
        var config = services.GetConfiguration();

        // 使用扩展方法添加认证服务
        services.AddXiHanAuthentication(config);
    }
}
