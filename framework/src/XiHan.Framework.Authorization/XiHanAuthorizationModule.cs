#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthorizationModule
// Guid:f08d98d7-2ff2-484b-aa45-acf3d88c0c09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/06 4:53:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authentication;
using XiHan.Framework.Authorization.Permissions;
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

        // 注册权限检查器
        services.AddScoped<IPermissionChecker, PermissionChecker>();

        // 注册授权服务
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        // 注意: 以下接口需要用户自己实现并注册
        // services.AddScoped<IPermissionStore, YourPermissionStoreImplementation>();
        // services.AddScoped<IRoleStore, YourRoleStoreImplementation>();
        // services.AddScoped<IRoleManager, YourRoleManagerImplementation>();
        // services.AddScoped<IPolicyStore, YourPolicyStoreImplementation>();
        // services.AddScoped<IPolicyEvaluator, YourPolicyEvaluatorImplementation>();
    }
}
