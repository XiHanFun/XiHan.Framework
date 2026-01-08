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
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;
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
        // 以下接口为默认实现，具体需要根据实际实现进行替换
        var services = context.Services;

        // 注册角色存储
        services.AddScoped<IRoleStore, DefaultRoleStore>();
        // 注册角色管理器
        services.AddScoped<IRoleManager, DefaultRoleManager>();
        // 注册权限存储
        services.AddScoped<IPermissionStore, DefaultPermissionStore>();
        // 注册权限检查器
        services.AddScoped<IPermissionChecker, DefaultPermissionChecker>();
        // 注册策略存储
        services.AddScoped<IPolicyStore, DefaultPolicyStore>();
        // 注册策略评估器
        services.AddScoped<IPolicyEvaluator, DefaultPolicyEvaluator>();
        // 注册授权服务
        services.AddScoped<IAuthorizationService, DefaultAuthorizationService>();
    }
}
